using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace OcrSharp.SiloHost
{
    public class Program
    {
        public static async Task Main(string[] args)
        {

            await CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureHostConfiguration(configHost =>
                {
                    configHost.SetBasePath(Directory.GetCurrentDirectory());
                    configHost.AddCommandLine(args);
                })
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.SetBasePath(Directory.GetCurrentDirectory());
                    configApp.AddJsonFile($"appsettings.json", true);
                    configApp.AddJsonFile($"appsettings.{hostContext}.json", true);
                    //configApp.AddJsonFile($"appsettings.{hostContext.HostingEnvironment}.json");
                    configApp.AddEnvironmentVariables();
                    configApp.AddUserSecrets<Program>();
                    configApp.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging();
                    services.AddHostedService<SiloHostService>();
                })
                .ConfigureLogging((hostContex, configLogging) =>
                {
                    configLogging.AddLog4Net("log4net.config");
                });
    }
}
