namespace Aurovel.GcsUpload.Services;

public interface IResumableUploader
{
    Task<(bool success, string? error, Uri? objectUri)> UploadAsync(
        Stream source,
        string fileName,
        string contentType,
        long totalLength,
        CancellationToken ct);
}