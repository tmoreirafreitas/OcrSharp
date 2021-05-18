using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OcrSharp.Api.Extensions;
using OcrSharp.Api.Middleware;
using OcrSharp.Api.Setup;
using OcrSharp.Domain.Options;
using OcrSharp.Infra.CrossCutting.IoC.Extensions;
using OcrSharp.Service.Hubs;
using Swashbuckle.AspNetCore.SwaggerUI;
using System;
using System.Linq;
using System.Reflection;

namespace OcrSharp.Api
{
    public class Startup
    {
        private IWritableOptions<TesseractOptions> _writableTesseract;
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
            });

            services.AddOptions();
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "OcrSharp.Api", Version = "v1" });
            });

            services.InstallServicesInAssembly(Configuration);
            services.AddSignalR(hubOptions =>
            {
                hubOptions.EnableDetailedErrors = true;
                hubOptions.KeepAliveInterval = TimeSpan.FromHours(24);
                hubOptions.MaximumReceiveMessageSize = 1073741824;
            })
            .AddJsonProtocol()
            .AddMessagePackProtocol();

            ConfigureOptions(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddLog4Net("log4net.config");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseStatusCodePages();
                app.UseHsts();
            }

            app.UseResponseCompression();
            app.UseStaticFiles();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.RoutePrefix = "swagger";
                c.SwaggerEndpoint("v1/swagger.json", "OcrSharp.Api v1");
                c.DocumentTitle = "OCR SHARP API DOCUMENTATION";
                c.DocExpansion(DocExpansion.None);
            });

            app.UseMiddleware(typeof(RequestMiddliware));
            app.UseMiddleware(typeof(GcMiddleware));
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<ImagesMessageHub>("/ImagesMessageHub");
            });
        }

        private void ConfigureOptions(IServiceCollection services)
        {
            services.ConfigureWritable<TesseractOptions>(Configuration.GetSection("Tesseract"));
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            ILoggerFactory loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Startup>();

            logger.LogInformation($"Configuring TesseractOptions ...");

            _writableTesseract = scope.ServiceProvider.GetRequiredService<IWritableOptions<TesseractOptions>>();
            _writableTesseract.Update(opt =>
            {
                const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance;
                var type = typeof(TesseractOptions);
                type.GetFields(bindingFlags).Cast<PropertyInfo>()
                    .Concat(type.GetProperties(bindingFlags))
                    .ToList()
                    .ForEach(field =>
                    {
                        var value = Environment.GetEnvironmentVariable(field.Name)
                                    ?? Environment.GetEnvironmentVariable(field.Name.ToLower())
                                    ?? Environment.GetEnvironmentVariable(field.Name.ToUpper())
                                    ?? string.Empty;

                        logger.LogInformation($"Get value in environment: Fiel = {field.Name}; Value = {value}");
                        if (!string.IsNullOrEmpty(value) || !string.IsNullOrWhiteSpace(value))
                            field.SetValue(opt, value);
                    });
            });

            logger.LogInformation($"TesseractOptions Configured ...");
        }
    }
}
