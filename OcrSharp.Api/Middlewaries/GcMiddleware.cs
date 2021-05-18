using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime;
using System.Threading.Tasks;

namespace OcrSharp.Api.Middleware
{
    public class GcMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public GcMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<GcMiddleware>();
        }

        public async Task Invoke(HttpContext httpContext)
        {
            await _next(httpContext);
            _logger.LogInformation(string.Format("Memory used before collection: {0:N0}", GC.GetTotalMemory(false)));
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(0, GCCollectionMode.Forced, true);
            GC.Collect(1, GCCollectionMode.Forced, true);
            GC.Collect(2, GCCollectionMode.Forced, true);

            GC.WaitForPendingFinalizers();

            GC.Collect(0, GCCollectionMode.Forced, true);
            GC.Collect(1, GCCollectionMode.Forced, true);
            GC.Collect(2, GCCollectionMode.Forced, true);

            // Collect all generations of memory.
            _logger.LogInformation("Memory used after full collection: {0:N0}", GC.GetTotalMemory(true));
        }
    }
}
