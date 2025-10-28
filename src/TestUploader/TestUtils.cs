
using System;
using System.IO;

namespace TestUploader
{
    public static class TestUtils
    {
        public static void GenerateTestFile(string filePath, long sizeInMb)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.SetLength(sizeInMb * 1024 * 1024);
            }
        }
    }
}
