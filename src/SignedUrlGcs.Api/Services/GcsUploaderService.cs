
using Google.Cloud.Storage.V1;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace SignedUrlGcs.Api.Services
{
    public class GcsUploaderService
    {
        private readonly StorageClient _storageClient;
        private readonly HttpClient _httpClient;

        public GcsUploaderService()
        {
            _storageClient = StorageClient.Create();
            _httpClient = new HttpClient();
        }

        public async Task<double> UploadStreamWithSignedUrlAsync(Stream fileStream, string bucketName, string objectName)
        {
            // IMPORTANT: Replace with the actual path to your Google Cloud credentials file.
            var urlSigner = UrlSigner.FromCredentialFile("service-account-credentials.json");

            var signedUrl = await urlSigner.SignAsync(
                bucketName,
                objectName,
                TimeSpan.FromMinutes(15),
                HttpMethod.Put
            );

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var streamContent = new StreamContent(fileStream);
            var response = await _httpClient.PutAsync(signedUrl, streamContent);

            response.EnsureSuccessStatusCode();

            stopwatch.Stop();

            return stopwatch.Elapsed.TotalSeconds;
        }
    }
}
