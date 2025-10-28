
using Google.Cloud.Storage.V1;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System;
using System.Threading.Tasks;

namespace SignedUrlGcs.Api.Services
{
    public class GcsUploaderService
    {
        private readonly UrlSigner _urlSigner;
        private readonly HttpClient _httpClient;

        public GcsUploaderService(UrlSigner urlSigner, HttpClient httpClient)
        {
            _urlSigner = urlSigner;
            _httpClient = httpClient;
        }

        public async Task<double> UploadStreamWithSignedUrlAsync(Stream fileStream, string bucketName, string objectName)
        {
            var signedUrl = await _urlSigner.SignAsync(
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
