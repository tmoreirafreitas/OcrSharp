using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace OcrSharp.SiloHost
{
    public class SiloHostService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private ISiloHost _siloHost;

        public SiloHostService(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<SiloHostService>();
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SiloHost running at: {time}", DateTimeOffset.Now);
            await StartSilo(stoppingToken);
        }

        private async Task StartSilo(CancellationToken stoppingToken)
        {
            _ = int.TryParse(_configuration["OcrSharpSiloHost:SiloPort"], out int siloPort);
            _ = int.TryParse(_configuration["OcrSharpSiloHost:GatewayPort"], out int gatewayPort);
            _ = int.TryParse(_configuration["OcrSharpSiloHost:UseDashboardPort"], out int dashboardPort);
            var invariant = "System.Data.SqlClient";
            var connectionString = _configuration.GetConnectionString("OcrSharpSiloHostConnection");
            var builder = new SiloHostBuilder()
                  // Use clustering for a silo
                  .UseAdoNetClustering(options =>
                  {
                      options.Invariant = invariant;
                      options.ConnectionString = connectionString;
                  })
                  .UseAdoNetReminderService(options =>
                  {
                      options.Invariant = invariant;
                      options.ConnectionString = connectionString;
                  })
                  //use AdoNet for Persistence
                  .AddAdoNetGrainStorage("OcrSharpSiloHostDb", options =>
                  {
                      options.Invariant = invariant;
                      options.ConnectionString = connectionString;
                      options.UseJsonFormat = true;
                  })
                  // Configure ClusterId and ServiceId
                  .Configure<ClusterOptions>(options =>
                  {
                      options.ClusterId = _configuration["OcrSharpSiloHost:ClusterId"];
                      options.ServiceId = _configuration["OcrSharpSiloHost:ServiceId"];
                  })
                  .Configure<SiloOptions>(options =>
                  {
                      options.SiloName = _configuration["OcrSharpSiloHost:SiloName"];
                  })
                  .Configure<GrainCollectionOptions>(options =>
                  {
                      options.CollectionAge = TimeSpan.FromMinutes(5);
                      options.CollectionQuantum = TimeSpan.FromMinutes(4);
                  })
                  // Configure connectivity
                  //.Configure<EndpointOptions>(options => options.AdvertisedIPAddress = IPAddress.Loopback)
                  .ConfigureEndpoints(siloPort: siloPort, gatewayPort: gatewayPort, addressFamily: AddressFamily.InterNetwork, listenOnAnyHostAddress: true)
                  //.ConfigureApplicationParts(parts => parts.AddFromApplicationBaseDirectory())
                  .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(DocumentGrain).Assembly).WithReferences())
                  .UseDashboard(options =>
                  {
                      options.Host = "*";
                      options.Port = dashboardPort;
                  })
                  // Configure logging with any logging framework that supports Microsoft.Extensions.Logging.
                  // In this particular case it logs using the Microsoft.Extensions.Logging.Console package.
                  .ConfigureLogging((hostContex, configLogging) =>
                  {
                      configLogging.AddConsole();
                      configLogging.AddLog4Net("log4net.config");
                  });
            _siloHost = builder.Build();
            await _siloHost.StartAsync(stoppingToken);
            _logger.LogInformation("Host started.");
        }
    }
}
