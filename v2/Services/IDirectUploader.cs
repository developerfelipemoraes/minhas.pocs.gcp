namespace Aurovel.GcsUpload.Services;

public interface IDirectUploader
{
    Task<(bool success, string? error, Uri? objectUri)> UploadAsync(
        Stream source,
        string fileName,
        string contentType,
        CancellationToken ct);

    Task<(bool success, string? error, Uri? objectUri)> UploadBytesAsync(
        byte[] data,
        string fileName,
        string contentType,
        CancellationToken ct);
}