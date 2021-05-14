using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace OcrSharp.Infra.CrossCutting.IoC
{
    public class TesseractInstaller : IInstaller
    {
        public void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            ConfigureEnvironmentTesseract(configuration);
        }

        private static void ConfigureEnvironmentTesseract(IConfiguration configuration)
        {
            string tessDataBest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata_best");
            string tessDataFast = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata_fast");
            string tessData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            configuration["Application:Tesseract:tessDataBest"] = tessDataBest;
            configuration["Application:Tesseract:tessDataFast"] = tessDataFast;
            configuration["Application:Tesseract:tessData"] = tessData;
        }
    }
}
