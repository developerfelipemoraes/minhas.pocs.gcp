
using Microsoft.AspNetCore.Mvc;
using SignedUrlGcs.Api.Services;
using System.Threading.Tasks;

namespace SignedUrlGcs.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DownloadController : ControllerBase
    {
        private readonly GcsDownloaderService _gcsDownloaderService;

        public DownloadController(GcsDownloaderService gcsDownloaderService)
        {
            _gcsDownloaderService = gcsDownloaderService;
        }

        [HttpGet("{bucketName}/{objectName}")]
        public async Task<IActionResult> Download(string bucketName, string objectName)
        {
            var stream = await _gcsDownloaderService.DownloadStreamAsync(bucketName, objectName);
            // It's important to leave the stream open so the framework can handle it.
            return new FileStreamResult(stream, "application/octet-stream")
            {
                FileDownloadName = objectName
            };
        }
    }
}
