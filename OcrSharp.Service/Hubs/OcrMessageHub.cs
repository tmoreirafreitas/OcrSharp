using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OcrSharp.Domain;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Hubs;
using OcrSharp.Domain.Interfaces.Services;
using System;
using System.Collections.Generic;
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
        private readonly IOpenCvService _openCvService;
        private readonly ILogger _logger;

        public OcrMessageHub(IFileUtilityService fileUtilityService, ITesseractService tesseractService,
                                IPdfToImageConverter pdfToImageConverter, IOpenCvService openCvService,
                                ILoggerFactory loggerFactory)
        {
            _pdfToImageConverter = pdfToImageConverter;
            _fileUtilityService = fileUtilityService;
            _tesseractService = tesseractService;
            _openCvService = openCvService;
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

        /// <summary>
        /// API de OCR de arquivos de JPG, PNG, GIF, TIFF e PDF
        /// </summary>
        /// <param name="pdf"></param>
        /// <param name="accuracy"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task ExtractTextFromPdf(PdfData pdf, Accuracy accuracy, string user)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            string mensagem;

            var tempPath = string.Empty;
            try
            {
                var pageCount = await _pdfToImageConverter.GetNumberOfPageAsync(pdf.Binary);
                DocumentFile docFile = null;
                IList<DocumentPage> docPages = null;
                var outputFilename = string.Empty;
                if (pageCount > 1)
                {
                    tempPath = await _pdfToImageConverter.ConvertToFilesPreProcessed(pdf.Binary, ".tif");
                    docPages = await _tesseractService.GetDocumentPages(tempPath, "*.tif", accuracy);
                    outputFilename = $"{await _fileUtilityService.NewTempFileName(tempPath)}.txt";
                    docFile = new DocumentFile(pageCount, Path.GetFileName(outputFilename));
                    docFile.Pages.AddRange(docPages.OrderBy(x => x.PageNumber));
                }
                else
                {
                    tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());
                    await _fileUtilityService.CreateFolder(tempPath);

                    var tempfile = $"{await _fileUtilityService.NewTempFileName(tempPath)}.tif";
                    using var ms = new MemoryStream(pdf.Binary);
                    var image = await _openCvService.ImageSmootheningAsync(new System.Drawing.Bitmap(ms));
                    image.Save(tempfile);

                    var result = await _tesseractService.GetText(tempfile, ".tif", accuracy);
                    docFile = new DocumentFile(pageCount, $"{Guid.NewGuid().ToString("N").ToUpper()}.txt");
                    docFile.Pages.Add(new DocumentPage(1, result, true));
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);

                docFile.RunTime = ts;
                _logger.LogInformation($"Sending processed file: {outputFilename} to the client: {user}");
                string jsonData = string.Format("{0}\n", JsonConvert.SerializeObject(docFile));
                await Clients.Client(user).OcrMessage(jsonData, StatusMessage.EXTRACTED_TEXT);

                mensagem = $"Total processing time: {elapsedTime}";
                _logger.LogInformation($"Sending message: {mensagem}");
                await Clients.Client(user).OcrMessage(mensagem, StatusMessage.FINISHED);

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