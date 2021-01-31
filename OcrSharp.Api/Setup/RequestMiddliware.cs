using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading.Tasks;


namespace OcrSharp.Api.Setup
{
    public class RequestMiddliware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public RequestMiddliware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<RequestMiddliware>();
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            int code;
            string result;

            if (exception is OperationCanceledException)
            {
                code = (int)HttpStatusCode.OK;
                result = JsonConvert.SerializeObject(new
                {
                    code,
                    title = "Atenção",
                    message = "A operação cancelada."
                });

                _logger.LogError(exception, "A operação cancelada.", null);
            }
            else
            {
                code = (int)HttpStatusCode.InternalServerError;
                result = JsonConvert.SerializeObject(new
                {
                    code,
                    title = "Atenção",
                    message = "Encontramos uma falha ao tentar realizar esta operação no momento"
                });

                _logger.LogError(exception, "Exceção não tratada.", null);
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = code;

            return context.Response.WriteAsync(result);
        }
    }
}
