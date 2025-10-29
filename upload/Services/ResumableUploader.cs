using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Aurovel.GcsUpload.Options;

namespace Aurovel.GcsUpload.Services;

public sealed class ResumableUploader : IResumableUploader
{
    private const int ChunkSize = 8 * 1024 * 1024; // 8 MiB (múltiplo de 256 KiB)
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGcsResumableSigner _signer;
    private readonly string _bucket;

    public ResumableUploader(
        IHttpClientFactory httpClientFactory,
        IGcsResumableSigner signer,
        IOptions<GcsOptions> gcsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _signer = signer;
        _bucket = gcsOptions.Value.Bucket;
    }

    public async Task<(bool success, string? error, Uri? objectUri)> UploadAsync(
        Stream source,
        string fileName,
        string contentType,
        long totalLength,
        CancellationToken ct)
    {
        var safeName = Path.GetFileName(fileName);
        var objectName = $"uploads/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}_{safeName}";
        var ctType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

        var http = _httpClientFactory.CreateClient();

        // 1) Assina URL para iniciar sessão resumível
        var initUrl = _signer.CreateResumableStartUrl(objectName, TimeSpan.FromMinutes(30));

        // 2) POST para iniciar a sessão (x-goog-resumable: start)
        var sessionUrl = await StartResumableSessionAsync(http, initUrl, ct);
        if (sessionUrl is null)
            return (false, "Falha ao iniciar sessão de upload (Location ausente).", null);

        // 3) Enviar chunks
        var (ok, error, status) = await UploadInChunksAsync(http, sessionUrl, source, ctType, totalLength, ct);
        if (!ok)
            return (false, error ?? $"Falha HTTP {(int?)status ?? 500}", null);

        // URL pública (se bucket permitir leitura pública)
        var publicUri = new Uri($"https://storage.googleapis.com/{_bucket}/{Uri.EscapeDataString(objectName)}");
        return (true, null, publicUri);
    }

    private static async Task<Uri?> StartResumableSessionAsync(HttpClient http, string initUrl, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, initUrl);
        req.Headers.TryAddWithoutValidation("x-goog-resumable", "start");

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode && resp.StatusCode != (HttpStatusCode)308)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Erro iniciando sessão resumível: {(int)resp.StatusCode} {resp.ReasonPhrase} - {body}");
        }

        if (resp.Headers.TryGetValues("Location", out var values))
        {
            var location = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(location) && Uri.TryCreate(location, UriKind.Absolute, out var uri))
                return uri;
        }
        return null;
    }

    private static async Task<(bool ok, string? error, HttpStatusCode? status)> UploadInChunksAsync(
        HttpClient http,
        Uri sessionUrl,
        Stream source,
        string contentType,
        long totalLength,
        CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        long position = 0;

        try
        {
            while (position < totalLength)
            {
                var remaining = totalLength - position;
                var toRead = (int)Math.Min(ChunkSize, remaining);
                int read = 0;

                while (read < toRead)
                {
                    var n = await source.ReadAsync(buffer.AsMemory(read, toRead - read), ct);
                    if (n == 0) break;
                    read += n;
                }

                if (read == 0) break; // fim do stream inesperado?

                var start = position;
                var end = position + read - 1;

                using var content = new ByteArrayContent(buffer, 0, read);
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, totalLength);

                using var put = new HttpRequestMessage(HttpMethod.Put, sessionUrl) { Content = content };
                using var resp = await http.SendAsync(put, HttpCompletionOption.ResponseHeadersRead, ct);

                if ((int)resp.StatusCode == 308) // Resume Incomplete
                {
                    position = end + 1;
                    continue;
                }

                if (resp.IsSuccessStatusCode) // 200/201 no último chunk
                {
                    position = end + 1;
                    break;
                }

                var body = await resp.Content.ReadAsStringAsync(ct);
                return (false, $"{(int)resp.StatusCode} {resp.ReasonPhrase} - {body}", resp.StatusCode);
            }

            var completed = position == totalLength;
            return (completed, completed ? null : "Upload incompleto (bytes enviados < total).", completed ? null : HttpStatusCode.PartialContent);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}