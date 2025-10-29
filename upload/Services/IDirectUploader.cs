namespace Aurovel.GcsUpload.Services;

public interface IDirectUploader
{
    /// <summary>
    /// Faz upload direto para o GCS usando Service Account, transmitindo do stream sem buffers gigantes.
    /// </summary>
    Task<(bool success, string? error, Uri? objectUri)> UploadAsync(
        Stream source,
        string fileName,
        string contentType,
        CancellationToken ct);

    /// <summary>
    /// Sobrecarga conveniente para quando você já tem um array de bytes em memória.
    /// </summary>
    Task<(bool success, string? error, Uri? objectUri)> UploadBytesAsync(
        byte[] data,
        string fileName,
        string contentType,
        CancellationToken ct);
}