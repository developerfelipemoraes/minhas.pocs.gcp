using System.Net.Http.Headers;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Net.Http.Headers;
using SignedUrlGcs.Api.Models;

namespace SignedUrlGcs.Api.Controllers;

[ApiController]
[Route("api")]
public class DownloadFileController : ControllerBase
{
    private readonly string _bucket;
    private readonly UrlSigner _signer;
    private readonly StorageClient _storage;
    private readonly IMemoryCache _cache;

    public DownloadFileController(IConfiguration cfg, StorageClient storage, IMemoryCache cache)
    {
        _bucket = cfg.GetValue<string>("Gcs:Bucket") ?? "filesmanager";

        var keyPath = cfg.GetValue<string>("Gcs:SignerKeyPath") ?? "gcs-signer.json";
        _signer = UrlSigner.FromCredentialFile(keyPath);

        _storage = storage;
        _cache = cache;
    }

    private async Task<Google.Apis.Storage.v1.Data.Object> GetObjectCachedAsync(string objectName, CancellationToken ct)
    {
        var cacheKey = $"gcs:obj:{_bucket}:{objectName}";
        if (_cache.TryGetValue(cacheKey, out Google.Apis.Storage.v1.Data.Object cached))
            return cached!;

        var obj = await _storage.GetObjectAsync(_bucket, objectName, cancellationToken: ct);
        var opts = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
        _cache.Set(cacheKey, obj, opts);
        return obj;
    }

    /// <summary>Lista objetos por prefixo (POC) — evite em requests quentes; prefira cache.</summary>
    [HttpGet]
    public ActionResult<IEnumerable<string>> List([FromQuery] string? prefix = "uploads/")
    {
        var results = new List<string>();
        foreach (var obj in _storage.ListObjects(_bucket, prefix))
            results.Add(obj.Name);
        return Ok(results);
    }

    /// <summary>Gera Signed URL V4 para download direto (GET) — caminho mais performático.</summary>
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

    /// <summary>HEAD para checagem rápida de existência/headers (players e aceleradores usam muito).</summary>
    [HttpHead("proxy")]
    public async Task<IActionResult> HeadProxy([FromQuery] string objectName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return BadRequest("objectName requerido.");
        
        var obj = await GetObjectCachedAsync(objectName, ct);

        var etag = obj.ETag?.Trim('"') ?? Guid.NewGuid().ToString("N");
        // Updated the code to use UpdatedDateTimeOffset instead of the obsolete Updated property.
        var lastModified = obj.UpdatedDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow;

        Response.Headers[HeaderNames.ETag] = $"\"{etag}\"";
        Response.Headers[HeaderNames.LastModified] = lastModified.ToString("R");
        Response.Headers[HeaderNames.AcceptRanges] = "bytes";
        Response.Headers[HeaderNames.ContentType] = obj.ContentType ?? "application/octet-stream";
        if (obj.Size.HasValue) Response.Headers[HeaderNames.ContentLength] = obj.Size.Value.ToString();

        return Ok();
    }

    /// <summary>Proxy de download via API (streaming) com Range (206) e 304.</summary>
    [HttpGet("proxy")]
    public async Task<IActionResult> Proxy([FromQuery] string objectName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return BadRequest("objectName requerido.");

        var obj = await GetObjectCachedAsync(objectName, ct);

        // Condicionais/Cache
        var etag = obj.ETag?.Trim('"') ?? Guid.NewGuid().ToString("N");
        var lastModified = DateTime.UtcNow;

        Response.Headers[HeaderNames.ETag] = $"\"{etag}\"";
        Response.Headers[HeaderNames.LastModified] = lastModified.ToString("R");
        Response.Headers[HeaderNames.AcceptRanges] = "bytes";

        if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var inm) && inm.ToString().Trim('"') == etag)
            return StatusCode(StatusCodes.Status304NotModified);
        if (Request.Headers.TryGetValue(HeaderNames.IfModifiedSince, out var ims) &&
            DateTime.TryParse(ims, out var imsDt) && imsDt >= lastModified)
            return StatusCode(StatusCodes.Status304NotModified);

        var total = (long?)obj.Size ?? -1;
        long? start = null; long? end = null;

        if (Request.Headers.TryGetValue(HeaderNames.Range, out var rangeHeader))
        {
            var range = System.Net.Http.Headers.RangeHeaderValue.Parse(rangeHeader!);
            var first = range.Ranges.First();
            start = first.From ?? 0;
            end = first.To ?? (total > 0 ? total - 1 : null);

            if (total > 0 && (start >= total || (end.HasValue && end >= total)))
                return StatusCode(StatusCodes.Status416RangeNotSatisfiable);

            Response.StatusCode = StatusCodes.Status206PartialContent;
            var contentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(start ?? 0, end ?? (total - 1), total);
            Response.Headers[HeaderNames.ContentRange] = contentRange.ToString();
            if (total > 0 && end.HasValue)
                Response.Headers[HeaderNames.ContentLength] = ((end.Value - (start ?? 0)) + 1).ToString();
        }
        else if (total > 0)
        {
            Response.Headers[HeaderNames.ContentLength] = total.ToString();
        }

        var fileName = Path.GetFileName(objectName);
   
        var cd = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileNameStar = fileName };
        Response.Headers[HeaderNames.ContentDisposition] = cd.ToString();
        Response.Headers[HeaderNames.ContentType] = obj.ContentType ?? "application/octet-stream";

        // Streaming sem buffers adicionais
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await Response.StartAsync(ct);

        var options = new DownloadObjectOptions
        {
            // ajuste 1–4 MiB conforme rede/CPU
            ChunkSize = 1 * 1024 * 1024,
            Range = (start.HasValue || end.HasValue) ? new System.Net.Http.Headers.RangeHeaderValue(start, end) : null
        };

        using var outStream = Response.BodyWriter.AsStream();
  
        await _storage.DownloadObjectAsync(_bucket, objectName, outStream, options, ct);
        
        return new EmptyResult();
    }
}