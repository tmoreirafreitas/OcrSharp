using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OcrSharp.Infra.CrossCutting.IoC.Extensions;
using System;
using System.IO;

namespace OcrSharp.Infra.CrossCutting.IoC
{
    public class RepositoryAndServiceInstaller : IInstaller
    {
        public void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.UseRepositoriesAndServices();
            CopyRequiredFileToCurrentDirectoryApp();
        }

        private void CopyRequiredFileToCurrentDirectoryApp()
        {
            string sourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Library\pdfium.dll");
            string destFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"pdfium.dll");

            if (!File.Exists(destFile))
                File.Copy(sourceFile, destFile, true);
        }        
    }
}
