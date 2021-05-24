using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OcrSharp.Domain;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Hubs;
using OcrSharp.Domain.Interfaces.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OcrSharp.Service.Hubs
{
    public class OcrMessageHub : Hub<IOcrMessageHub>
    {
        private readonly IFileUtilityService _fileUtilityService;
        private readonly IPdfToImageConverter _pdfToImageConverter;
        private readonly ITesseractService _tesseractService;
        private readonly ILogger _logger;

        public OcrMessageHub(IFileUtilityService fileUtilityService, ITesseractService tesseractService,
                                IPdfToImageConverter pdfToImageConverter, ILoggerFactory loggerFactory)
        {
            _pdfToImageConverter = pdfToImageConverter;
            _fileUtilityService = fileUtilityService;
            _tesseractService = tesseractService;
            _logger = loggerFactory.CreateLogger<OcrMessageHub>();
        }

        public async override Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            _logger.LogInformation($"A client connected to ImagesMessageHub: {Context.ConnectionId}");
        }

        public async override Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
            _logger.LogInformation($"A client disconnected from ImagesMessageHub: {Context.ConnectionId}");
        }

        public async Task ExtractTextFromPdf(PdfData pdf, Accuracy accuracy, string user)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            string mensagem;

            var tempPath = string.Empty;
            try
            {
                var pageCount = await _pdfToImageConverter.GetNumberOfPageAsync(pdf.Binary);
                tempPath = await _pdfToImageConverter.ConvertToFilesPreProcessed(pdf.Binary, ".tif");

                var docPages = await _tesseractService.GetDocumentPages(tempPath, "*.tif", accuracy);
                var outputFilename = $"{await _fileUtilityService.NewTempFileName(tempPath)}.txt";
                var docFile = new DocumentFile(pageCount, Path.GetFileName(outputFilename));
                docFile.Pages.AddRange(docPages.OrderBy(x => x.PageNumber));

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);

                docFile.RunTime = ts;
                _logger.LogInformation($"Sending processed file: {outputFilename} to the client: {user}");
                string jsonData = string.Format("{0}\n", JsonConvert.SerializeObject(docFile));
                await Clients.Client(user).ImageMessage(jsonData, StatusMensagem.TEXTO_EXTRAIDO);

                mensagem = $"Total processing time: {elapsedTime}";
                _logger.LogInformation($"Sending message: {mensagem}");
                await Clients.Client(user).ImageMessage(mensagem, StatusMensagem.FINALIZADO);

                docPages.Clear();
                docPages = null;
                docFile.Dispose();
                docFile = null;
                jsonData = null;
                mensagem = null;
                outputFilename = null;
                user = null;
            }
            finally
            {
                await _fileUtilityService.DeleteAllAsync(tempPath, true);
                tempPath = null;
            }
        }
    }
}