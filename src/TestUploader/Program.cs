
using SignedUrlGcs.Api.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TestUploader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var uploader = new GcsUploaderService();
            var totalUploadTime = 0.0;
            // IMPORTANT: Replace with your GCS bucket name.
            var bucketName = "your-bucket-name";

            for (int i = 1; i <= 10; i++)
            {
                var fileName = $"test-file-{i}.tmp";
                var objectName = $"test-file-{i}";

                Console.WriteLine($"Generating file [{i}/10] (100 MB)...");
                TestUtils.GenerateTestFile(fileName, 100);

                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    Console.WriteLine($"Uploading file [{i}/10]...");
                    var uploadTime = await uploader.UploadStreamWithSignedUrlAsync(fs, bucketName, objectName);
                    totalUploadTime += uploadTime;
                    Console.WriteLine($"Arquivo [{i}/10] (100 MB) - Tempo de Upload: [{uploadTime:F2}] segundos.");
                }

                File.Delete(fileName);
            }

            Console.WriteLine($"Tempo Total de Upload (10 arquivos / 1 GB): [{totalUploadTime:F2}] segundos.");
        }
    }
}
