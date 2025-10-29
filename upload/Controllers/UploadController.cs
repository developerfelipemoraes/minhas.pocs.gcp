using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Aurovel.GcsUpload.Services;

namespace Aurovel.GcsUpload.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UploadController : ControllerBase
{
    private readonly IResumableUploader _uploader;
    private readonly IDirectUploader _direct;

    public UploadController(IResumableUploader uploader, IDirectUploader direct)
    {
        _uploader = uploader;
        _direct = direct;
    }

    // ====== Assinada (Resumable via Signed URL) ======
    [HttpPost("resumable")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 1L * 1024 * 1024 * 1024)] // 1 GB
    public async Task<IActionResult> Resumable([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Arquivo ausente ou vazio." });

        await using var stream = file.OpenReadStream();

        var (success, error, publicUri) = await _uploader.UploadAsync(
            stream,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            file.Length,
            ct);

        if (!success)
            return Problem(title: "Falha no upload resumível", detail: error, statusCode: 500);

        return Ok(new
        {
            file = file.FileName,
            size = file.Length,
            contentType = file.ContentType,
            mediaLink = publicUri!.ToString()
        });
    }

    /// <summary>
    /// ====== Direto (byte array / stream) ======
    /// Upload DIRETO para o GCS usando Service Account (sem Signed URL).
    /// Envie o corpo como application/octet-stream (raw bytes).
    /// Informe o nome do arquivo via header X-File-Name (opcional) ou via query (?fileName=&contentType=).
    /// </summary>
    [HttpPost("direct-bytes")]
    [DisableRequestSizeLimit]
    [RequestSizeLimit(long.MaxValue)]
    public async Task<IActionResult> DirectBytes([FromQuery] string? fileName, [FromQuery] string? contentType, CancellationToken ct)
    {
        var resolvedFileName = !string.IsNullOrWhiteSpace(fileName)
            ? fileName!
            : (Request.Headers.TryGetValue("X-File-Name", out var hName) ? hName.ToString() : $"upload_{DateTime.UtcNow:yyyyMMdd_HHmmss}");

        var resolvedContentType = !string.IsNullOrWhiteSpace(contentType) ? contentType! : (Request.ContentType ?? "application/octet-stream");

        // NÃO usa Base64. Lê o corpo como stream e envia ao GCS.
        var (success, error, publicUri) = await _direct.UploadAsync(Request.Body, resolvedFileName, resolvedContentType, ct);

        if (!success)
            return Problem(title: "Falha no upload direto", detail: error, statusCode: 500);

        return Ok(new
        {
            file = resolvedFileName,
            contentType = resolvedContentType,
            mediaLink = publicUri!.ToString()
        });
    }
}