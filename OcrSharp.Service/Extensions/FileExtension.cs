using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OcrSharp.Service.Extensions
{
    public static class FileExtension
    {
        public static async Task SaveFileOnTempDirectoryAndRun(this IFormFile file, Action<string> action)
        {
            var filePath = await file.SaveFileOnTempDirectory();

            try
            {
                action(filePath);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        public static async Task<string> SaveFileOnTempDirectory(this IFormFile file)
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():D}-{file.FileName}");

            using (var stream = File.OpenWrite(filePath))
            {
                await file.CopyToAsync(stream);
            }

            return filePath;
        }

        public static async Task<string> SaveFileOnTempDirectory(this Stream fileImage, string filename)
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():D}-{filename}");

            using (var stream = File.OpenWrite(filePath))
            {
                await fileImage.CopyToAsync(stream);
            }

            return filePath;
        }
    }
}
