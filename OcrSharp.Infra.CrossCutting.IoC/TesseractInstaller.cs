using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;

namespace OcrSharp.Infra.CrossCutting.IoC
{
    public class TesseractInstaller : IInstaller
    {
        public void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            DownloadAndExtractLanguagePack(configuration);
        }

        private static string DownloadAndExtractLanguagePack(IConfiguration configuration)
        {
            //Source path to the zip file
            string langPackPath = configuration["Tesseract:langPackPath"];

            string zipFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata.zip");
            string tessDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            configuration["Tesseract:tessDataFolder"] = tessDataFolder;

            //Check and download the source file
            if (!File.Exists(zipFileName))
            {
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(langPackPath, zipFileName);
                }
            }

            //Extract the zip to tessdata folder
            if (!Directory.Exists(tessDataFolder))
            {
                ZipFile.ExtractToDirectory(zipFileName, AppDomain.CurrentDomain.BaseDirectory);
                var extractedDir = Directory.EnumerateDirectories(AppDomain.CurrentDomain.BaseDirectory).FirstOrDefault(x => x.Contains("tessdata"));
                Directory.Move(extractedDir, tessDataFolder);
            }

            return tessDataFolder;
        }
    }
}
