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
        }      
    }
}
