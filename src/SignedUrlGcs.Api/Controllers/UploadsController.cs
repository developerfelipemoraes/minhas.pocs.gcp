using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Mvc;
using SignedUrlGcs.Api.Models;

namespace SignedUrlGcs.Api.Controllers;

[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    private readonly string _bucket;
    private readonly UrlSigner _signer;

    public UploadsController(IConfiguration cfg)
    {
        _bucket = "filesmanager";
        var keyPath = "gcs-signer.json"; 
        _signer = UrlSigner.FromCredentialFile(keyPath);
    }

    /// <summary>
    /// Gera Signed URL V4 para iniciar sessão de upload resumable (x-goog-resumable:start).
    /// </summary>
    [HttpPost()]
    public async Task<ActionResult<CreateSignedUrlResponse>> Post([FromBody] CreateSignedUrlRequest req)
    {
        var objectName = string.IsNullOrWhiteSpace(req.ObjectName)
            ? $"uploads/{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bin"
            : req.ObjectName.Trim();

        var ttl = TimeSpan.FromHours (5);

        var template = UrlSigner.RequestTemplate
            .FromBucket(_bucket)
            .WithObjectName(objectName)
            .WithHttpMethod(UrlSigner.ResumableHttpMethod);

        var url = await _signer.SignAsync(template, UrlSigner.Options.FromDuration(ttl));

        return Ok(new CreateSignedUrlResponse(url, objectName, DateTimeOffset.UtcNow.Add(ttl)));
    }
}
