
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using SignedUrlGcs.Api.Services;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace TestUploader
{
    class Program
    {
        private const string BucketName = "your-gcs-bucket-name"; // <-- IMPORTANT: Replace with your GCS bucket name.

        static async Task Main(string[] args)
        {
            var credential = GoogleCredential.GetApplicationDefault();
            var urlSigner = UrlSigner.FromCredential(credential);
            var httpClient = new HttpClient();
            var uploader = new GcsUploaderService(urlSigner, httpClient);
            var totalUploadTime = 0.0;

            Console.WriteLine($"--- Starting GCS Upload Benchmark ---");
            Console.WriteLine($"Target Bucket: {BucketName}");
            Console.WriteLine($"Files to Upload: 10 x 100 MB");
            Console.WriteLine("-------------------------------------");

            for (int i = 1; i <= 10; i++)
            {
                var fileName = $"test-file-{i}.tmp";
                var objectName = $"benchmark/test-file-{i}";

                try
                {
                    Console.Write($"Generating file {i}/10 (100 MB)... ");
                    TestUtils.GenerateTestFile(fileName, 100);
                    Console.WriteLine("Done.");

                    using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                    {
                        Console.Write($"Uploading file {i}/10... ");
                        var uploadTime = await uploader.UploadStreamWithSignedUrlAsync(fs, BucketName, objectName);
                        totalUploadTime += uploadTime;
                        Console.WriteLine($"Success! Time: {uploadTime:F2} seconds.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading file {i}: {ex.Message}");
                }
                finally
                {
                    if (File.Exists(fileName))
                    {
                        File.Delete(fileName);
                    }
                }
            }

            Console.WriteLine("-------------------------------------");
            Console.WriteLine($"Benchmark Complete!");
            Console.WriteLine($"Total Upload Time (10 files / 1 GB): {totalUploadTime:F2} seconds.");
            Console.WriteLine($"Average Upload Time per file: {totalUploadTime / 10:F2} seconds.");
            Console.WriteLine("-------------------------------------");
        }
    }
}
