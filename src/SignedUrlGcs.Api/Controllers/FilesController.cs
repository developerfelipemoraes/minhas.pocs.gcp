using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SignedUrlGcs.Api.Models;

namespace SignedUrlGcs.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly string _bucket;
    private readonly UrlSigner _signer;
    private readonly StorageClient _storage;

    public FilesController(IConfiguration cfg, StorageClient storage)
    {
        _bucket = "filesmanager";

        var keyPath = "gcs-signer.json";

        _signer = UrlSigner.FromCredentialFile(keyPath);

        _storage = storage;
    }

    /// <summary>Lista objetos por prefixo (POC).</summary>
    [HttpGet()]
    public ActionResult<IEnumerable<string>> List([FromQuery] string? prefix = "uploads/")
    {
        var results = new List<string>();

        foreach (var obj in _storage.ListObjects(_bucket, prefix))
        {
            results.Add(obj.Name);
        }

        return Ok(results);
    }

    /// <summary>Gera Signed URL V4 para download (GET).</summary>
    [HttpGet("signed-download")]
    public async Task<ActionResult<SignedDownloadResponse>> SignedDownload([FromQuery] string objectName, [FromQuery] int ttlMinutes = 10)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return BadRequest("objectName requerido.");
        var ttl = TimeSpan.FromMinutes(ttlMinutes);

        var template = UrlSigner.RequestTemplate
            .FromBucket(_bucket)
            .WithObjectName(objectName)
            .WithHttpMethod(HttpMethod.Get);

        var url = await _signer.SignAsync(template, UrlSigner.Options.FromDuration(ttl));
        return Ok(new SignedDownloadResponse(url, DateTimeOffset.UtcNow.Add(ttl)));
    }

    /// <summary>Proxy de download via API (streaming).</summary>
    [HttpGet("proxy")]
    public async Task<IActionResult> Proxy([FromQuery] string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return BadRequest("objectName requerido.");

        var obj = await _storage.GetObjectAsync(_bucket, objectName);
       
        Response.Headers[HeaderNames.ContentType] = obj.ContentType ?? "application/octet-stream";
        
        Response.Headers[HeaderNames.ContentDisposition] = $"attachment; filename='{Path.GetFileName(objectName)}'";

        await _storage.DownloadObjectAsync(_bucket, objectName, Response.Body);
        
        return new EmptyResult();
    }
}
