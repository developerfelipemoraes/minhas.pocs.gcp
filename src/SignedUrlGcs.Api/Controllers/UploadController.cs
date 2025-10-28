
using Microsoft.AspNetCore.Http;
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

        [HttpPost("stream/{bucketName}/{objectName}")]
        public async Task<IActionResult> UploadRawStream(string bucketName, string objectName)
        {
            var uploadTime = await _gcsUploaderService.UploadStreamWithSignedUrlAsync(Request.Body, bucketName, objectName);
            return Ok(new { UploadTimeInSeconds = uploadTime });
        }

        [HttpPost("form/{bucketName}")]
        public async Task<IActionResult> UploadFormFile(string bucketName, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("File is required.");
            }

            using var stream = file.OpenReadStream();
            var uploadTime = await _gcsUploaderService.UploadStreamWithSignedUrlAsync(stream, bucketName, file.FileName);
            return Ok(new { FileName = file.FileName, UploadTimeInSeconds = uploadTime });
        }
    }
}
