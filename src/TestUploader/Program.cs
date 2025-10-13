using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static System.Net.WebRequestMethods;

/* ================ MODO INTERATIVO (Console.ReadLine) ================ */
// Se não houver args, ou se tiver --interactive, abre um "wizard" no console.
if (args.Length == 0 || args.Contains("--interactive"))
{
    Console.WriteLine("=====Testes apenas Download de arquivos ===========");
    Console.WriteLine("=====Deseja testar apenas o processo de download (S/N): ===========");
    
    var answerTesteDownload = Console.ReadLine();

    if (answerTesteDownload == "S")
    {
        var settingsDownload = await PromptDownloadsSettingsAsync();
        Console.WriteLine();
        Console.WriteLine("==== RESUMO DAS CONFIGURAÇÕES ====");
        
        DumpSettingsDownload(settingsDownload);
        
        Console.WriteLine();
        
        await RunsDownloadAsync(settingsDownload);
        
        return 0;
    }
    else 
    {
        var s = await PromptSettingsAsync();
        Console.WriteLine();
        Console.WriteLine("==== RESUMO DAS CONFIGURAÇÕES ====");
        DumpSettings(s);
        Console.WriteLine();
        if (s.Debug)
        {
            Console.Write("Debug ligado: pressione ENTER para iniciar os testes...");
            Console.ReadLine();
        }
        await RunAsync(s);
        return 0;
    }
  
}

/* ====================== MODO POR FLAGS (como antes) ================== */
var apiBaseOpt = new Option<string>("--api-base", "Base da sua API de upload (ex.: https://host/api/uploads)") { IsRequired = true };
var tokenOpt = new Option<string?>("--token", "Bearer token para chamar sua API (opcional)");
var bucketOpt = new Option<string?>("--bucket", () => "filesmanager", "Bucket (usado na leitura, se informada)");
var prefixOpt = new Option<string?>("--prefix", () => $"perf/{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}", "Prefixo para agrupar objetos");
var downloadsOpt = new Option<string?>("--download-base", "Base do endpoint de leitura (ex.: https://host/v1/files). Se omitido, download é pulado.");
var ctOpt = new Option<string?>("--content-type", () => "application/octet-stream", "Content-Type do upload");
var concOpt = new Option<int>("--concurrency", () => 1, "Uploads paralelos (1 = sequencial)");
var filesOpt = new Option<int>("--files", () => 10, "Arquivos por tamanho");
var debugOpt = new Option<bool>("--debug", () => false, "Log detalhado de cada etapa");
var verifyDlOpt = new Option<bool>("--verify-download", () => true, "Faz download para medir throughput");

var root = new RootCommand("TestUploader - 10 tamanhos (5 MiB → 50 MiB), N arquivos por tamanho") {
    apiBaseOpt, tokenOpt, bucketOpt, prefixOpt, ctOpt, downloadsOpt, concOpt, filesOpt, debugOpt, verifyDlOpt
};

root.SetHandler(async (InvocationContext ctx) =>
{
    var s = new Settings(
        ApiBase: ctx.ParseResult.GetValueForOption(apiBaseOpt)!,
        Token: ctx.ParseResult.GetValueForOption(tokenOpt),
        Bucket: ctx.ParseResult.GetValueForOption(bucketOpt),
        Prefix: ctx.ParseResult.GetValueForOption(prefixOpt),
        DownloadBase: ctx.ParseResult.GetValueForOption(downloadsOpt),
        ContentType: ctx.ParseResult.GetValueForOption(ctOpt) ?? "application/octet-stream",
        Concurrency: ctx.ParseResult.GetValueForOption(concOpt),
        FilesPerSize: ctx.ParseResult.GetValueForOption(filesOpt),
        Debug: ctx.ParseResult.GetValueForOption(debugOpt),
        VerifyDownload: ctx.ParseResult.GetValueForOption(verifyDlOpt)
    );
    DumpSettings(s);
    if (s.Debug)
    {
        Console.Write("Debug ligado: pressione ENTER para iniciar...");
        Console.ReadLine();
    }
    await RunAsync(s);
});

return await root.InvokeAsync(args);

/*****PIPELINE DE DOWNLOAD ARQUIVOS*******/
static async Task RunsDownloadAsync(SettingsDownload settingsDownload)
{
    var http = CreateHttpClient();

    var uploaded = new List<(string objectName, int sizeMiB)>();

    var summaryUpload = new List<(int sizeMiB, int files, double avgMbps, double p95Ms)>();

    var filesBucket = await RequestListObjectsBucket(http, 
        settingsDownload.ApiBase, 
        settingsDownload.Token, 
        settingsDownload.Prefix, 
        settingsDownload.Debug);

    foreach (var file in filesBucket)
    {
        await DownloadAsync(
            apiBase: settingsDownload.ApiBase,
            objectName: file,
            token: null,    
            outputPath: Environment.CurrentDirectory
        );

    }
    //{
    //    Console.WriteLine($"\n=== Upload - tamanho {sizeMiB} MiB ({s.FilesPerSize} arquivos) ===");

    //    var objectsThisSize = new List<(string obj, long bytes, double ms, double mbps)>();
    //    var tasks = new List<Task>();
    //    var locker = new object();

    //    for (int i = 1; i <= s.FilesPerSize; i++)
    //    {
    //        var iCopy = i;
    //        var task = Task.Run(async () =>
    //        {
    //            var objectName = $"{s.Prefix}/size-{sizeMiB}MiB/file-{iCopy:D2}-{Guid.NewGuid():N}.bin";

    //            // 1) Pede Signed URL V4 (resumable) ao /api/uploads
    //            var signed = await RequestSignedUrlAsync(http, s.ApiBase, s.Token, objectName, s.Debug);

    //            // 2) Inicia sessão resumable no GCS
    //            var sessionUri = await InitiateResumableAsync(http, signed.Url, sizeMiB, s.ContentType, s.Debug);

    //            // 3) Upload em 1 único chunk (PUT com Content-Range)
    //            var (elapsedMs, mbps) = await UploadSingleChunkAsync(http, sessionUri, sizeMiB, s.ContentType, s.Debug);

    //            lock (locker)
    //            {
    //                objectsThisSize.Add((objectName, (long)sizeMiB * 1024 * 1024, elapsedMs, mbps));
    //                uploaded.Add((objectName, sizeMiB));
    //            }
    //            Console.WriteLine($"UP ✓ {objectName}  {sizeMiB}MiB  {mbps:F2} Mb/s  ({elapsedMs:F0} ms)");
    //        });

    //        tasks.Add(task);
    //        if (s.Concurrency <= 1)
    //            await task; // sequencial
    //        else if (tasks.Count >= s.Concurrency)
    //        {
    //            await Task.WhenAll(tasks);
    //            tasks.Clear();
    //        }
    //    }
    //    if (tasks.Count > 0) await Task.WhenAll(tasks);

    //    // Estatística simples por tamanho
    //    var avgMbps = objectsThisSize.Count > 0 ? objectsThisSize.Average(o => o.mbps) : 0;

    //    var p95 = Percentile(objectsThisSize.Select(o => o.ms).ToArray(), 0.95);

    //    summaryUpload.Add((sizeMiB, objectsThisSize.Count, avgMbps, p95));

    //    Console.WriteLine($"-- Upload resumo {sizeMiB}MiB: média {avgMbps:F2} Mb/s  p95 {p95:F0} ms");

    //    if (s.Debug)
    //    {
    //        Console.Write("Pausar antes do próximo tamanho? (s/N): ");
    //        if (ReadBoolDefaultNo(Console.ReadLine()))
    //            Console.Write("ENTER para continuar..."); Console.ReadLine();
    //    }
    //}

    //// (Opcional) Downloads via endpoint de leitura
    //if (!string.IsNullOrWhiteSpace(s.DownloadBase) && s.VerifyDownload)
    //{
    //    Console.WriteLine("\n### Iniciando testes de DOWNLOAD ###");
    //    var summaryDownload = new List<(int sizeMiB, int files, double avgMbps, double p95Ms)>();

    //    foreach (var group in uploaded.GroupBy(x => x.sizeMiB).OrderBy(g => g.Key))
    //    {
    //        var sizeMiB = group.Key;
    //        var files = group.ToList();
    //        var results = new List<(double ms, double mbps)>();

    //        foreach (var f in files)
    //        {
    //            var url = $"{s.DownloadBase!.TrimEnd('/')}/{Uri.EscapeDataString(f.objectName)}?bucket={Uri.EscapeDataString(s.Bucket ?? "filesmanager")}";
    //            var (ms, mbps) = await DownloadMeasureAsync(CreateHttpClient(), url, s.Debug); // client novo p/ aproximar mundo real
    //            results.Add((ms, mbps));
    //            Console.WriteLine($"DL ✓ {f.objectName}  {sizeMiB}MiB  {mbps:F2} Mb/s  ({ms:F0} ms)");
    //        }

    //        var avg = results.Count > 0 ? results.Average(r => r.mbps) : 0;
    //        var p95 = Percentile(results.Select(r => r.ms).ToArray(), 0.95);
    //        summaryDownload.Add((sizeMiB, results.Count, avg, p95));
    //        Console.WriteLine($"-- Download resumo {sizeMiB}MiB: média {avg:F2} Mb/s  p95 {p95:F0} ms");
    //    }

    //    Console.WriteLine("\n===== SUMÁRIO FINAL =====");
    //    Console.WriteLine("UPLOAD");
    //    foreach (var s1 in summaryUpload.OrderBy(su => su.sizeMiB))
    //        Console.WriteLine($"  {s1.sizeMiB,3} MiB: {s1.files,2} arquivos | avg {s1.avgMbps:F2} Mb/s | p95 {s1.p95Ms:F0} ms");

    //    Console.WriteLine("\nDOWNLOAD");
    //    foreach (var s2 in summaryDownload.OrderBy(sd => sd.sizeMiB))
    //        Console.WriteLine($"  {s2.sizeMiB,3} MiB: {s2.files,2} arquivos | avg {s2.avgMbps:F2} Mb/s | p95 {s2.p95Ms:F0} ms");
    //}
    //else
    //{
    //    Console.WriteLine("\n(downloads pulados — use --download-base para medir também o caminho de leitura)");
    //    Console.WriteLine("\n===== SUMÁRIO FINAL (UPLOAD) =====");
    //    foreach (var s1 in summaryUpload.OrderBy(su => su.sizeMiB))
    //        Console.WriteLine($"  {s1.sizeMiB,3} MiB: {s1.files,2} arquivos | avg {s1.avgMbps:F2} Mb/s | p95 {s1.p95Ms:F0} ms");
    //}
}

/** ********/


/* ========================= PIPELINE PRINCIPAL ========================= */
static async Task RunAsync(Settings s)
{
    var http = CreateHttpClient();

    // 10 tamanhos exponenciais 5 → 50 MiB
    var sizes = BuildExponentialSizesMiB(5, 50, 10);
    Console.WriteLine($"\n-> Prefixo: {s.Prefix}");
    Console.WriteLine($"-> Tamanhos (MiB): {string.Join(", ", sizes)}");
    Console.WriteLine($"-> Arquivos por tamanho: {s.FilesPerSize}");
    Console.WriteLine($"-> Concurrency: {s.Concurrency}");
    Console.WriteLine();

    var uploaded = new List<(string objectName, int sizeMiB)>();
    var summaryUpload = new List<(int sizeMiB, int files, double avgMbps, double p95Ms)>();

    foreach (var sizeMiB in sizes)
    {
        Console.WriteLine($"\n=== Upload - tamanho {sizeMiB} MiB ({s.FilesPerSize} arquivos) ===");

        var objectsThisSize = new List<(string obj, long bytes, double ms, double mbps)>();
        var tasks = new List<Task>();
        var locker = new object();

        for (int i = 1; i <= s.FilesPerSize; i++)
        {
            var iCopy = i;
            var task = Task.Run(async () =>
            {
                var objectName = $"{s.Prefix}/size-{sizeMiB}MiB/file-{iCopy:D2}-{Guid.NewGuid():N}.bin";

                // 1) Pede Signed URL V4 (resumable) ao /api/uploads
                var signed = await RequestSignedUrlAsync(http, s.ApiBase, s.Token, objectName, s.Debug);

                // 2) Inicia sessão resumable no GCS
                var sessionUri = await InitiateResumableAsync(http, signed.Url, sizeMiB, s.ContentType, s.Debug);

                // 3) Upload em 1 único chunk (PUT com Content-Range)
                var (elapsedMs, mbps) = await UploadSingleChunkAsync(http, sessionUri, sizeMiB, s.ContentType, s.Debug);

                lock (locker)
                {
                    objectsThisSize.Add((objectName, (long)sizeMiB * 1024 * 1024, elapsedMs, mbps));
                    uploaded.Add((objectName, sizeMiB));
                }
                Console.WriteLine($"UP ✓ {objectName}  {sizeMiB}MiB  {mbps:F2} Mb/s  ({elapsedMs:F0} ms)");
            });

            tasks.Add(task);
            if (s.Concurrency <= 1)
                await task; // sequencial
            else if (tasks.Count >= s.Concurrency)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
        }
        if (tasks.Count > 0) await Task.WhenAll(tasks);

        // Estatística simples por tamanho
        var avgMbps = objectsThisSize.Count > 0 ? objectsThisSize.Average(o => o.mbps) : 0;

        var p95 = Percentile(objectsThisSize.Select(o => o.ms).ToArray(), 0.95);
        
        summaryUpload.Add((sizeMiB, objectsThisSize.Count, avgMbps, p95));
        
        Console.WriteLine($"-- Upload resumo {sizeMiB}MiB: média {avgMbps:F2} Mb/s  p95 {p95:F0} ms");

        if (s.Debug)
        {
            Console.Write("Pausar antes do próximo tamanho? (s/N): ");
            if (ReadBoolDefaultNo(Console.ReadLine()))
                Console.Write("ENTER para continuar..."); Console.ReadLine();
        }
    }

    // (Opcional) Downloads via endpoint de leitura
    if (!string.IsNullOrWhiteSpace(s.DownloadBase) && s.VerifyDownload)
    {
        Console.WriteLine("\n### Iniciando testes de DOWNLOAD ###");
        var summaryDownload = new List<(int sizeMiB, int files, double avgMbps, double p95Ms)>();

        foreach (var group in uploaded.GroupBy(x => x.sizeMiB).OrderBy(g => g.Key))
        {
            var sizeMiB = group.Key;
            var files = group.ToList();
            var results = new List<(double ms, double mbps)>();

            foreach (var f in files)
            {
                var url = $"{s.DownloadBase!.TrimEnd('/')}/{Uri.EscapeDataString(f.objectName)}?bucket={Uri.EscapeDataString(s.Bucket ?? "filesmanager")}";
                var (ms, mbps) = await DownloadMeasureAsync(CreateHttpClient(), url, s.Debug); // client novo p/ aproximar mundo real
                results.Add((ms, mbps));
                Console.WriteLine($"DL ✓ {f.objectName}  {sizeMiB}MiB  {mbps:F2} Mb/s  ({ms:F0} ms)");
            }

            var avg = results.Count > 0 ? results.Average(r => r.mbps) : 0;
            var p95 = Percentile(results.Select(r => r.ms).ToArray(), 0.95);
            summaryDownload.Add((sizeMiB, results.Count, avg, p95));
            Console.WriteLine($"-- Download resumo {sizeMiB}MiB: média {avg:F2} Mb/s  p95 {p95:F0} ms");
        }

        Console.WriteLine("\n===== SUMÁRIO FINAL =====");
        Console.WriteLine("UPLOAD");
        foreach (var s1 in summaryUpload.OrderBy(su => su.sizeMiB))
            Console.WriteLine($"  {s1.sizeMiB,3} MiB: {s1.files,2} arquivos | avg {s1.avgMbps:F2} Mb/s | p95 {s1.p95Ms:F0} ms");

        Console.WriteLine("\nDOWNLOAD");
        foreach (var s2 in summaryDownload.OrderBy(sd => sd.sizeMiB))
            Console.WriteLine($"  {s2.sizeMiB,3} MiB: {s2.files,2} arquivos | avg {s2.avgMbps:F2} Mb/s | p95 {s2.p95Ms:F0} ms");
    }
    else
    {
        Console.WriteLine("\n(downloads pulados — use --download-base para medir também o caminho de leitura)");
        Console.WriteLine("\n===== SUMÁRIO FINAL (UPLOAD) =====");
        foreach (var s1 in summaryUpload.OrderBy(su => su.sizeMiB))
            Console.WriteLine($"  {s1.sizeMiB,3} MiB: {s1.files,2} arquivos | avg {s1.avgMbps:F2} Mb/s | p95 {s1.p95Ms:F0} ms");
    }
}

static async Task DownloadAsync(string apiBase, string objectName, string? token, string outputPath)
{
    using var http = new HttpClient();
    if (!string.IsNullOrWhiteSpace(token))
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var url = $"{apiBase.TrimEnd('/')}/api/files/proxy?objectName={Uri.EscapeDataString(objectName)}";
    using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
    resp.EnsureSuccessStatusCode();

    // tenta pegar o nome do servidor; fallback = nome do objeto
    var cd = resp.Content.Headers.ContentDisposition;
    var fileName = cd?.FileNameStar ?? cd?.FileName?.Trim('"') ?? Path.GetFileName(objectName);
    var dest = Path.Combine(outputPath, fileName);

    using var input = await resp.Content.ReadAsStreamAsync();
    using var output = System.IO.File.Create(dest);

    var buffer = new byte[128 * 1024];
    long totalRead = 0;
    var len = resp.Content.Headers.ContentLength;
    int read;

    while ((read = await input.ReadAsync(buffer)) > 0)
    {
        await output.WriteAsync(buffer.AsMemory(0, read));
        totalRead += read;
        if (len.HasValue)
        {
            Console.Write($"\rBaixado {totalRead:n0}/{len:n0} bytes ({(totalRead * 100.0 / len.Value):0.0}%)");
        }
        else
        {
            Console.Write($"\rBaixado {totalRead:n0} bytes");
        }
    }

    Console.WriteLine($"\nOK: {dest}");
}


/* ========================= helpers comuns ========================= */

static HttpClient CreateHttpClient()
{
    var handler = new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 512,
        EnableMultipleHttp2Connections = true,
        KeepAlivePingDelay = TimeSpan.FromSeconds(30),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(10)
    };

    return new HttpClient(handler)
    {
        Timeout = TimeSpan.FromMinutes(10),
        DefaultRequestVersion = HttpVersion.Version20,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
    };
}

static int[] BuildExponentialSizesMiB(int startMiB, int endMiB, int count)
{
    var sizes = new int[count];
    double r = Math.Pow((double)endMiB / startMiB, 1.0 / (count - 1));
    for (int i = 0; i < count; i++)
        sizes[i] = (int)Math.Round(startMiB * Math.Pow(r, i));
    sizes[count - 1] = endMiB;
    return sizes;
}

static async Task<(double ms, double mbps)> UploadSingleChunkAsync(HttpClient http, Uri sessionUri, int sizeMiB, string contentType, bool debug)
{
    long bytes = (long)sizeMiB * 1024 * 1024;
    using var content = new ByteArrayContent(GetBuffer(sizeMiB));
    content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
    content.Headers.ContentLength = bytes;

    using var put = new HttpRequestMessage(HttpMethod.Put, sessionUri);
    put.Headers.TryAddWithoutValidation("Content-Range", $"bytes 0-{bytes - 1}/{bytes}");
    put.Content = content;

    if (debug)
        Console.WriteLine($"[DBG] PUT {sessionUri}  Content-Range: bytes 0-{bytes - 1}/{bytes}");

    var sw = Stopwatch.StartNew();
    using var resp = await http.SendAsync(put, HttpCompletionOption.ResponseHeadersRead);
    sw.Stop();
    if (debug) Console.WriteLine($"[DBG] ← {(int)resp.StatusCode} ({sw.ElapsedMilliseconds} ms)");
    resp.EnsureSuccessStatusCode();

    var mbps = (bytes * 8.0) / (sw.Elapsed.TotalSeconds * 1_000_000.0);
    return (sw.Elapsed.TotalMilliseconds, mbps);
}

static async Task<Uri> InitiateResumableAsync(HttpClient http, string signedUrl, int sizeMiB, string contentType, bool debug)
{
    using var init = new HttpRequestMessage(HttpMethod.Post, signedUrl);
    init.Headers.TryAddWithoutValidation("x-goog-resumable", "start");
    init.Headers.TryAddWithoutValidation("X-Upload-Content-Type", contentType);
    init.Headers.TryAddWithoutValidation("X-Upload-Content-Length", ((long)sizeMiB * 1024L * 1024L).ToString());

    if (debug) Console.WriteLine($"[DBG] POST (init resumable) {signedUrl}");

    using var resp = await http.SendAsync(init);
    if (debug) Console.WriteLine($"[DBG] ← {(int)resp.StatusCode}");
    if (resp.StatusCode != HttpStatusCode.Created && resp.StatusCode != HttpStatusCode.OK)
    {
        var body = await resp.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Falha ao iniciar resumable ({(int)resp.StatusCode}). Body: {body}");
    }
    var loc = resp.Headers.Location ?? (resp.Headers.TryGetValues("Location", out var vals) ? new Uri(vals.First()) : null);
    if (loc is null) throw new InvalidOperationException("Resposta sem Location (session URI).");
    if (debug) Console.WriteLine($"[DBG] Session URI: {loc}");
    return loc;
}


static async Task<(double ms, double mbps)> DownloadMeasureAsync(HttpClient http, string url, bool debug)
{
    using var req = new HttpRequestMessage(HttpMethod.Get, url);

    if (debug) Console.WriteLine($"[DBG] GET {url}");
    var sw = Stopwatch.StartNew();
    
    using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
    
    resp.EnsureSuccessStatusCode();

    await using var stream = await resp.Content.ReadAsStreamAsync();
    byte[] buf = new byte[64 * 1024];
    long total = 0;
    int read;
    while ((read = await stream.ReadAsync(buf)) > 0) total += read;

    sw.Stop();
    var mbps = (total * 8.0) / (sw.Elapsed.TotalSeconds * 1_000_000.0);
    if (debug) Console.WriteLine($"[DBG] ← {(int)resp.StatusCode} {total} bytes ({sw.ElapsedMilliseconds} ms)");
    return (sw.Elapsed.TotalMilliseconds, mbps);
}

static async Task<List<string>> RequestListObjectsBucket(
    HttpClient http, string apiBase, string? token, string objectName, bool debug)
{

    var url =$"{apiBase.TrimEnd('/')}/list?prefix={WebUtility.UrlEncode(objectName)}&pageSize=50";

    using var req = new HttpRequestMessage(HttpMethod.Get, url);

    if (!string.IsNullOrWhiteSpace(token))
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    if (debug)
    {
        var tail = string.IsNullOrEmpty(token) ? "" : token[^Math.Min(12, token.Length)..];
        Console.WriteLine($"[DBG] POST {req.RequestUri}  Auth=Bearer …{tail} ");
    }
    
    var sw = Stopwatch.StartNew();

    using var responseListFiles = await http.SendAsync(req);

    sw.Stop();

    if (debug) Console.WriteLine($"[DBG] ← {(int) responseListFiles.StatusCode} ({sw.ElapsedMilliseconds} ms)");

    responseListFiles.EnsureSuccessStatusCode();

    var json = await responseListFiles.Content.ReadAsStringAsync();
    
    if (debug) Console.WriteLine($"[DBG] Body: {json}");

    try
    {
        using var doc = JsonDocument.Parse(json);
        
        var root = doc.RootElement;

        List<string> listFiles = JsonSerializer.Deserialize<List<string>>(json);

        return listFiles;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Falha ao interpretar JSON da URL assinada: {ex.Message}. Body: {json}", ex);
    }
}

static async Task<(string Url, string ObjectName, DateTimeOffset ExpiresAtUtc)> RequestSignedUrlAsync(
    HttpClient http, string apiBase, string? token, string objectName, bool debug)
{
    using var req = new HttpRequestMessage(HttpMethod.Post, apiBase.TrimEnd('/'));
   
    if (!string.IsNullOrWhiteSpace(token))
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var payload = JsonSerializer.Serialize(new { objectName });
  
    req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

    if (debug)
    {
        var tail = string.IsNullOrEmpty(token) ? "" : token[^Math.Min(12, token.Length)..];
        Console.WriteLine($"[DBG] POST {req.RequestUri}  Auth=Bearer …{tail}  Body={payload}");
    }

    var sw = Stopwatch.StartNew();
    
    using var resp = await http.SendAsync(req);
    
    sw.Stop();

    if (debug) Console.WriteLine($"[DBG] ← {(int)resp.StatusCode} ({sw.ElapsedMilliseconds} ms)");
    resp.EnsureSuccessStatusCode();


    var json = await resp.Content.ReadAsStringAsync();
    if (debug) Console.WriteLine($"[DBG] Body: {json}");

    try
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // aceita várias chaves comuns
        var url = TryGetStringCI(root, "url")
                ?? TryGetStringCI(root, "signedUrl")
                ?? TryGetStringCI(root, "uploadUrl")
                ?? TryGetStringCI(root, "resumableUrl")
                ?? throw new KeyNotFoundException("Campo de URL não encontrado (url/signedUrl/uploadUrl/resumableUrl).");

        var obj = TryGetStringCI(root, "objectName")
                ?? TryGetStringCI(root, "object")
                ?? objectName;

        var exp = TryGetDateTimeOffsetCI(root, "expiresAtUtc")
               ?? TryGetDateTimeOffsetCI(root, "expires")
               ?? DateTimeOffset.UtcNow.AddMinutes(15);

        return (url, obj, exp);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Falha ao interpretar JSON da URL assinada: {ex.Message}. Body: {json}", ex);
    }
}

static byte[] GetBuffer(int sizeMiB)
{
    var bytes = new byte[sizeMiB * 1024 * 1024];
    for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i * 31 + 7);
    return bytes;
}

static double Percentile(double[] values, double p)
{
    if (values.Length == 0) return 0;
    Array.Sort(values);
    double rank = p * (values.Length - 1);
    int lo = (int)Math.Floor(rank);
    int hi = (int)Math.Ceiling(rank);
    if (lo == hi) return values[lo];
    return values[lo] + (values[hi] - values[lo]) * (rank - lo);
}

/* ========================= helpers de JSON ========================= */

static string? TryGetStringCI(JsonElement e, string name)
{
    foreach (var p in e.EnumerateObject())
        if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            return p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
    return null;
}

static DateTimeOffset? TryGetDateTimeOffsetCI(JsonElement e, string name)
{
    foreach (var p in e.EnumerateObject())
        if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            if (p.Value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(p.Value.GetString(), out var dto))
                return dto;
            if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt64(out var unix))
                return DateTimeOffset.FromUnixTimeSeconds(unix);
        }
    return null;
}

/* ========================= modo interativo ========================= */

static async Task<SettingsDownload> PromptDownloadsSettingsAsync()
{
    Console.WriteLine("== TestUploader (modo interativo) ==");

    var apiBase = ReadRequired("API base (ex.: https://host/api/downloads): ");

    while (!Uri.TryCreate(apiBase, UriKind.Absolute, out _))
        apiBase = ReadRequired("URL inválida. Informe a API base novamente: ");

    var token = ReadOptional("Bearer token (ENTER para vazio): ");

    var bucket = ReadOptional("Bucket para leitura (ENTER p/ 'filesmanager'): ", "filesmanager");

    var prefix = ReadOptional($"Prefixo (ENTER p/ perf/{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}): ", $"perf/{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}");
    
    // Só para simular alguma latência de leitura de inputs
    await Task.CompletedTask;

    return new SettingsDownload(
        ApiBase: apiBase,
        Token: token,
        Bucket: bucket,
        Prefix: prefix
    );
}

static async Task<Settings> PromptSettingsAsync()
{
    Console.WriteLine("== TestUploader (modo interativo) ==");
  
    var apiBase = ReadRequired("API base (ex.: https://host/api/uploads): ");
    
    while (!Uri.TryCreate(apiBase, UriKind.Absolute, out _))
        apiBase = ReadRequired("URL inválida. Informe a API base novamente: ");

    
    var token = ReadOptional("Bearer token (ENTER para vazio): ");
    
    var bucket = ReadOptional("Bucket para leitura (ENTER p/ 'filesmanager'): ", "filesmanager");
    
    var prefix = ReadOptional($"Prefixo (ENTER p/ perf/{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}): ", $"perf/{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}");
    
    var downloadBase = ReadOptional("Base do endpoint de leitura (ENTER para pular downloads): ");
    
    string contentType;
    do
    {
        contentType = ReadOptional("Content-Type (ENTER p/ application/octet-stream): ", "application/octet-stream")!;
    } while (string.IsNullOrWhiteSpace(contentType));

    var concurrency = ReadInt("Concurrency (1 = sequencial) [1..512] (ENTER p/ 1): ", 1, min: 1, max: 512);
    
    var filesPerSize = ReadInt("Arquivos por tamanho (ENTER p/ 10): ", 10, min: 1, max: 10000);
    
    var debug = ReadYesNo("Debug detalhado? (s/N): ", def: false);
    
    var verifyDownload = string.IsNullOrWhiteSpace(downloadBase) ? false : ReadYesNo("Verificar DOWNLOAD também? (S/n): ", def: true);

    // Só para simular alguma latência de leitura de inputs
    await Task.CompletedTask;

    return new Settings(
        ApiBase: apiBase,
        Token: token,
        Bucket: bucket,
        Prefix: prefix,
        DownloadBase: string.IsNullOrWhiteSpace(downloadBase) ? null : downloadBase,
        ContentType: contentType!,
        Concurrency: concurrency,
        FilesPerSize: filesPerSize,
        Debug: debug,
        VerifyDownload: verifyDownload
    );
}

static string ReadRequired(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        var s = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
        Console.WriteLine("Valor obrigatório.");
    }
}

static string? ReadOptional(string prompt, string? def = null)
{
    Console.Write(prompt);
    var s = Console.ReadLine();
    if (string.IsNullOrEmpty(s)) return def;
    return s.Trim();
}

static int ReadInt(string prompt, int def, int min, int max)
{
    while (true)
    {
        Console.Write(prompt);
        var s = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(s)) return def;
        if (int.TryParse(s, out var v) && v >= min && v <= max) return v;
        Console.WriteLine($"Informe um inteiro entre {min} e {max}.");
    }
}

static bool ReadYesNo(string prompt, bool def)
{
    Console.Write(prompt);
    var s = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
    if (string.IsNullOrEmpty(s)) return def;
    return s is "s" or "sim" or "y" or "yes";
}

static bool ReadBoolDefaultNo(string? s)
{
    s = (s ?? "").Trim().ToLowerInvariant();
    return s is "s" or "sim" or "y" or "yes";
}


static void DumpSettingsDownload(SettingsDownload settingsDownload)
{
    Console.WriteLine($"apiBase       : {settingsDownload.ApiBase}");
    Console.WriteLine($"token         : {(string.IsNullOrWhiteSpace(settingsDownload.Token) ? "(vazio)" : "*****...")}");
    Console.WriteLine($"bucket        : {settingsDownload.Bucket}");
    Console.WriteLine($"prefix        : {settingsDownload.Prefix}");
}

static void DumpSettings(Settings s)
{
    Console.WriteLine($"apiBase       : {s.ApiBase}");
    Console.WriteLine($"token         : {(string.IsNullOrWhiteSpace(s.Token) ? "(vazio)" : "*****...")}");
    Console.WriteLine($"bucket        : {s.Bucket}");
    Console.WriteLine($"prefix        : {s.Prefix}");
    Console.WriteLine($"downloadBase  : {s.DownloadBase ?? "(pulado)"}");
    Console.WriteLine($"contentType   : {s.ContentType}");
    Console.WriteLine($"concurrency   : {s.Concurrency}");
    Console.WriteLine($"filesPerSize  : {s.FilesPerSize}");
    Console.WriteLine($"debug         : {s.Debug}");
    Console.WriteLine($"verifyDownload: {s.VerifyDownload}");
}

/* ========================= settings DTO ========================= */

record Settings(
    string ApiBase,
    string? Token,
    string? Bucket,
    string? Prefix,
    string? DownloadBase,
    string ContentType,
    int Concurrency,
    int FilesPerSize,
    bool Debug,
    bool VerifyDownload
);

record SettingsDownload(
    string ApiBase,
    string? Token,
    string? Bucket,
    string? Prefix, 
    bool Debug = true
);
