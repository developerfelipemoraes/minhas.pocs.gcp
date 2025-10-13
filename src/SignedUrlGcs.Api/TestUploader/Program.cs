
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var apiBaseOpt   = new Option<string>("--api-base", "Base da sua API de upload (ex.: https://host/api/uploads)") { IsRequired = true };
var tokenOpt     = new Option<string?>("--token", "Bearer token para chamar sua API (opcional)");
var bucketOpt    = new Option<string?>("--bucket", () => "filesmanager", "Bucket (usado na leitura, se informada)");
var prefixOpt    = new Option<string?>("--prefix", () => $"perf/{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}", "Prefixo para agrupar objetos");
var downloadsOpt = new Option<string?>("--download-base", "Base do endpoint de leitura (ex.: https://host/v1/files). Se omitido, download é pulado.");
var ctOpt        = new Option<string?>("--content-type", () => "application/octet-stream", "Content-Type do upload");
var concOpt      = new Option<int>("--concurrency", () => 1, "Uploads paralelos (1 = sequencial)");
var filesOpt     = new Option<int>("--files", () => 10, "Arquivos por tamanho (padrão 10, conforme seu cenário)");
var debugOpt     = new Option<bool>("--debug", () => false, "Log detalhado de cada etapa");
var verifyDlOpt  = new Option<bool>("--verify-download", () => true, "Faz download para medir throughput");

var root = new RootCommand("TestUploader - 10 tamanhos (5 MiB → 50 MiB), N arquivos por tamanho (padrão: 10)") {
    apiBaseOpt, tokenOpt, bucketOpt, prefixOpt, ctOpt, downloadsOpt, concOpt, filesOpt, debugOpt, verifyDlOpt
};

root.SetHandler(async (InvocationContext ctx) =>
{
    var apiBase        = ctx.ParseResult.GetValueForOption(apiBaseOpt)!;
    var token          = ctx.ParseResult.GetValueForOption(tokenOpt);
    var bucket         = ctx.ParseResult.GetValueForOption(bucketOpt);
    var prefix         = ctx.ParseResult.GetValueForOption(prefixOpt);
    var contentType    = ctx.ParseResult.GetValueForOption(ctOpt);
    var downloadBase   = ctx.ParseResult.GetValueForOption(downloadsOpt);
    var concurrency    = ctx.ParseResult.GetValueForOption(concOpt);
    var filesPerSize   = ctx.ParseResult.GetValueForOption(filesOpt);
    var debug          = ctx.ParseResult.GetValueForOption(debugOpt);
    var verifyDownload = ctx.ParseResult.GetValueForOption(verifyDlOpt);

    var http = CreateHttpClient();

    // 10 tamanhos exponenciais 5 → 50 MiB
    var sizes = BuildExponentialSizesMiB(5, 50, 10);
    Console.WriteLine($"-> Prefixo: {prefix}");
    Console.WriteLine($"-> Tamanhos (MiB): {string.Join(", ", sizes)}");
    Console.WriteLine($"-> Arquivos por tamanho: {filesPerSize}");
    Console.WriteLine();

    var uploaded = new List<(string objectName, int sizeMiB)>();
    var summaryUpload = new List<(int sizeMiB, int files, double avgMbps, double p95Ms)>();

    foreach (var sizeMiB in sizes)
    {
        Console.WriteLine($"\n=== Upload - tamanho {sizeMiB} MiB ({filesPerSize} arquivos) ===");

        var objectsThisSize = new List<(string obj, long bytes, double ms, double mbps)>();

        var tasks = new List<Task>();
        var locker = new object();

        for (int i = 1; i <= filesPerSize; i++)
        {
            var iCopy = i;
            var task = Task.Run(async () =>
            {
                var objectName = $"{prefix}/size-{sizeMiB}MiB/file-{iCopy:D2}-{Guid.NewGuid():N}.bin";

                // 1) Pede Signed URL V4 (resumable) ao /api/uploads
                var signed = await RequestSignedUrlAsync(http, apiBase, token, objectName, debug);
                // 2) Inicia sessão resumable no GCS
                var sessionUri = await InitiateResumableAsync(http, signed.Url, sizeMiB, contentType!, debug);
                // 3) Upload em 1 único chunk (PUT com Content-Range)
                var (elapsedMs, mbps) = await UploadSingleChunkAsync(http, sessionUri, sizeMiB, contentType!, debug);

                lock (locker)
                {
                    objectsThisSize.Add((objectName, (long)sizeMiB * 1024 * 1024, elapsedMs, mbps));
                    uploaded.Add((objectName, sizeMiB));
                }
                Console.WriteLine($"UP ✓ {objectName}  {sizeMiB}MiB  {mbps:F2} Mb/s  ({elapsedMs:F0} ms)");
            });

            tasks.Add(task);
            if (concurrency <= 1)
                await task; // sequencial simples
            else if (tasks.Count >= concurrency)
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
    }

    // (Opcional) Downloads via endpoint de leitura
    if (!string.IsNullOrWhiteSpace(downloadBase) && verifyDownload)
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
                var url = $"{downloadBase!.TrimEnd('/')}/{Uri.EscapeDataString(f.objectName)}?bucket={Uri.EscapeDataString(bucket!)}";
                var (ms, mbps) = await DownloadMeasureAsync(http, url, debug);
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
        foreach (var s in summaryUpload.OrderBy(s => s.sizeMiB))
            Console.WriteLine($"  {s.sizeMiB,3} MiB: {s.files,2} arquivos | avg {s.avgMbps:F2} Mb/s | p95 {s.p95Ms:F0} ms");

        Console.WriteLine("\nDOWNLOAD");
        foreach (var s in summaryDownload.OrderBy(s => s.sizeMiB))
            Console.WriteLine($"  {s.sizeMiB,3} MiB: {s.files,2} arquivos |Mb/s | p95 {s.p95Ms:F0} ms");
    }
    else
    {
        Console.WriteLine("\n(downloads pulados — use --download-base para medir também o caminho de leitura)");
        Console.WriteLine("\n===== SUMÁRIO FINAL (UPLOAD) =====");
        foreach (var s in summaryUpload.OrderBy(s => s.sizeMiB))
            Console.WriteLine($"  {s.sizeMiB,3} MiB: {s.files,2} arquivos | avg {s.avgMbps:F2} Mb/s | p95 {s.p95Ms:F0} ms");
    }

});

return await root.InvokeAsync(args);

/* ========================= helpers ========================= */

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

static async Task<(string Url, string ObjectName, DateTimeOffset ExpiresAtUtc)> RequestSignedUrlAsync(HttpClient http, string apiBase, string? token, string objectName, bool debug)
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

    using var doc = JsonDocument.Parse(json);
    string url = doc.RootElement.GetPropertyCaseInsensitive("url").GetString()!;
    string obj = doc.RootElement.GetPropertyCaseInsensitive("objectName").GetString()!;
    var exp = doc.RootElement.GetPropertyCaseInsensitive("expiresAtUtc").GetDateTimeOffset();

    return (url, obj, exp);
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

static class JsonExt
{
    public static JsonElement GetPropertyCaseInsensitive(this JsonElement e, string name)
    {
        foreach (var p in e.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        throw new KeyNotFoundException(name);
    }
}
