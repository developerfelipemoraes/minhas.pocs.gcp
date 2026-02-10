using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using Aurovel.GcsUpload.Options;

namespace Aurovel.GcsUpload.Services;

public sealed class DirectUploader : IDirectUploader
{
    private readonly string _bucket;
    private readonly StorageClient _storage;

    public DirectUploader(IOptions<GcsOptions> options)
    {
        var cfg = options.Value;
        _bucket = cfg.Bucket ?? throw new ArgumentNullException(nameof(cfg.Bucket));

        GoogleCredential cred;
        if (!string.IsNullOrWhiteSpace(cfg.ServiceAccountJson))
        {
            cred = GoogleCredential.FromJson(cfg.ServiceAccountJson);
        }
        else if (!string.IsNullOrWhiteSpace(cfg.ServiceAccountKeyPath))
        {
            cred = GoogleCredential.FromFile(cfg.ServiceAccountKeyPath!);
        }
        else
        {
            throw new InvalidOperationException("Credenciais GCP não configuradas (ServiceAccountJson ou ServiceAccountKeyPath).");
        }

        _storage = StorageClient.Create(cred);
    }

    public async Task<(bool success, string? error, Uri? objectUri)> UploadAsync(
        Stream source,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        try
        {
            var safeName = Path.GetFileName(fileName);
            var objectName = $"uploads/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}_{safeName}";

            var options = new UploadObjectOptions
            {
                ChunkSize = 8 * 1024 * 1024 // 8 MiB
            };

            var obj = await _storage.UploadObjectAsync(
                bucket: _bucket,
                objectName: objectName,
                contentType: string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                source: source,
                options: options,
                cancellationToken: ct);

            var publicUri = new Uri($"https://storage.googleapis.com/{_bucket}/{Uri.EscapeDataString(obj.Name)}");
            return (true, null, publicUri);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool success, string? error, Uri? objectUri)> UploadBytesAsync(
        byte[] data,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        await using var ms = new MemoryStream(data, writable: false);
        return await UploadAsync(ms, fileName, contentType, ct);
    }
}