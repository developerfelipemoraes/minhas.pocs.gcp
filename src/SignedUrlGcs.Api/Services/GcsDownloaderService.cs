
using Google.Cloud.Storage.V1;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SignedUrlGcs.Api.Services
{
    public class GcsDownloaderService
    {
        private readonly StorageClient _storageClient;
        private readonly HttpClient _httpClient;

        public GcsDownloaderService(StorageClient storageClient, HttpClient httpClient)
        {
            _storageClient = storageClient;
            _httpClient = httpClient;
        }

        public async Task<Stream> DownloadStreamAsync(string bucketName, string objectName)
        {
            var storageObject = await _storageClient.GetObjectAsync(bucketName, objectName);
            var response = await _httpClient.GetAsync(storageObject.MediaLink, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }
    }
}
