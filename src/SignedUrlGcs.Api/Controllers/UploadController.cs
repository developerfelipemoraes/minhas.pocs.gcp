
using Microsoft.AspNetCore.Mvc;
using SignedUrlGcs.Api.Services;
using System.Threading.Tasks;

namespace SignedUrlGcs.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly GcsUploaderService _gcsUploaderService;

        public UploadController(GcsUploaderService gcsUploaderService)
        {
            _gcsUploaderService = gcsUploaderService;
        }

        [HttpPost("{bucketName}/{objectName}")]
        public async Task<IActionResult> Upload(string bucketName, string objectName)
        {
            var uploadTime = await _gcsUploaderService.UploadStreamWithSignedUrlAsync(Request.Body, bucketName, objectName);
            return Ok(new { UploadTimeInSeconds = uploadTime });
        }
    }
}
