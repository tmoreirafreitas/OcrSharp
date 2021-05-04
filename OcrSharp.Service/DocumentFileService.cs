using Docnet.Core;
using Docnet.Core.Converters;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OcrSharp.Domain;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Hubs;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service.Extensions;
using OcrSharp.Service.Hubs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OcrSharp.Service
{
    public class DocumentFileService : IDocumentFileService
    {
        private readonly IFileUtilityService _fileUtilityService;
        private readonly IOcrFileService _ocrFileService;
        private readonly IHubContext<ImagesMessageHub, IStreaming> _hubContext;
        private readonly ILogger _logger;

        public DocumentFileService(IFileUtilityService fileUtilityService, IOcrFileService ocrFileService,
            ILoggerFactory loggerFactory, IHubContext<ImagesMessageHub, IStreaming> hubContext)
        {
            _fileUtilityService = fileUtilityService;
            _ocrFileService = ocrFileService;
            _hubContext = hubContext;
            _logger = loggerFactory.CreateLogger<DocumentFileService>();
        }

        public async Task<InMemoryFile> ConvertMultiplePdfToImageAsync(IEnumerable<InMemoryFile> fileCollection, CancellationToken cancellationToken = default(CancellationToken))
        {
            var filesToZip = new List<InMemoryFile>();
            await fileCollection.ParallelForEachAsync(async file =>
            {
                if (cancellationToken.IsCancellationRequested)
                    cancellationToken.ThrowIfCancellationRequested();

                var fileZipStream = await ConvertPdfFileToImagesZippedAsync(file);

                filesToZip.Add(new InMemoryFile
                {
                    FileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_Images_Results_{DateTime.Now.GetDateNowEngFormat()}.zip",
                    Content = fileZipStream.ConvertToArray(cancellationToken)
                });
            }, Environment.ProcessorCount);

            var zipFile = await _fileUtilityService.GetZipArchive(filesToZip, cancellationToken);
            var filename = $"IMAGES_RESULTS_{DateTime.Now.GetDateNowEngFormat()}.zip";
            return new InMemoryFile
            {
                FileName = filename,
                Content = zipFile.ConvertToArray(cancellationToken)
            };
        }

        public async Task<DocumentPage> ExtracTextFromPdfPageAsync(InMemoryFile file, int pageNumber, Accuracy accuracy = Accuracy.Low, CancellationToken cancellationToken = default(CancellationToken))
        {
            using var doc = DocLib.Instance.GetDocReader(file.Content, new PageDimensions(1080, 1920));
            var pdfPage = doc.GetPageReader(pageNumber);
            var currentText = pdfPage.GetText();
            bool applyedOcr = false;
            DocumentPage page = null;

            if (!string.IsNullOrEmpty(currentText) && !string.IsNullOrWhiteSpace(currentText))
            {
                currentText = Encoding.UTF8.GetString(Encoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(currentText)));
                page = new DocumentPage(pageNumber, currentText, applyedOcr);
            }
            else
            {
                var tiffStream = ConvertToTiffByteArray(pdfPage).ToStream();
                if (tiffStream != null)
                {
                    var ocrResult = await _ocrFileService.ApplyOcrAsync(tiffStream, accuracy);
                    currentText = Encoding.UTF8.GetString(Encoding.Convert(Encoding.Default, Encoding.UTF8, ocrResult.Content));
                    applyedOcr = true;
                    page = new DocumentPage(pageNumber, currentText, applyedOcr)
                    {
                        Accuracy = ocrResult.Accuracy,
                        RunTime = ocrResult.RunTime
                    };
                }
            }

            return page;
        }

        public int GetNumberOfPages(InMemoryFile file)
        {
            using var doc = DocLib.Instance.GetDocReader(file.Content, new PageDimensions(1080, 1920));
            var numberOfPages = doc.GetPageCount();
            return numberOfPages;
        }

        public async Task<DocumentFile> ExtractTextFromPdf(string connectionId, InMemoryFile file, Accuracy accuracy)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            _hubContext.Clients.Client(connectionId).ImageMessage($"Iniciando extração do texto do documento: {file.FileName} em {DateTime.Now:dd/MM/yyyy HH:mm:ss.ff}", StatusMensagem.INFORMATIVO).Wait();

            using var doc = DocLib.Instance.GetDocReader(file.Content, new PageDimensions(1080, 1920));
            var numberOfPages = doc.GetPageCount();

            var pdf = new DocumentFile(numberOfPages, file.FileName);
            var maxthreads = Convert.ToInt32(Math.Ceiling(Environment.ProcessorCount * 0.75 * 7.0));
            var images = new List<InMemoryFile>();
            var totalPages = Enumerable.Range(0, numberOfPages);
            var locker = new object();
            await totalPages.ParallelForEachAsync(numberPage =>
            {
                var currentPage = numberPage + 1;
                var pageReader = doc.GetPageReader(numberPage);
                var rawBytes = ConvertToTiffByteArray(pageReader);
                lock (locker)
                {
                    images.Add(new InMemoryFile { Page = currentPage, Content = rawBytes, FileName = file.FileName });

                    stopWatch.Stop();

                    TimeSpan el = stopWatch.Elapsed;
                    string strElapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    el.Hours, el.Minutes, el.Seconds,
                    el.Milliseconds / 10);

                    _hubContext.Clients.Client(connectionId).ImageMessage($"Foram convertidas {images.Count} de {numberOfPages} páginas em imagem. Tempo decorrido: {strElapsedTime}", StatusMensagem.INFORMATIVO);
                    stopWatch.Start();
                }
                return Task.CompletedTask;
            }, maxthreads);

            var ocrResult = await _ocrFileService.ApplyOcrAsync(connectionId, images, accuracy);

            foreach (var ocr in ocrResult)
            {
                var currentText = Encoding.UTF8.GetString(ocr.Content);
                pdf.Pages.Add(new DocumentPage(ocr.Page, currentText, ocr.AppliedOcr)
                {
                    Accuracy = ocr.Accuracy,
                    RunTime = ocr.RunTime
                });
            }

            stopWatch.Stop();
            return pdf;
        }

        public async Task<Stream> ConvertPdfFileToImagesZippedAsync(InMemoryFile file)
        {
            using var doc = DocLib.Instance.GetDocReader(file.Content, new PageDimensions(1080, 1920));
            var files = new List<InMemoryFile>();

            var index = 0;
            var images = ConvertToListByteArray(doc);
            foreach (var rawData in images)
            {
                var pdfFilename = $"{ Path.GetFileNameWithoutExtension(file.FileName)}_Pagina_{++index:D3}.tiff";
                files.Add(new InMemoryFile
                {
                    FileName = pdfFilename,
                    Content = rawData
                });
            }

            return await _fileUtilityService.GetZipArchive(files);
        }

        public InMemoryFile ConvertPdfPageToImageAsync(InMemoryFile file, int pageNumber)
        {
            var pdfFilename = $"{ Path.GetFileNameWithoutExtension(file.FileName)}_Pagina_{pageNumber:D3}.tiff";
            using var doc = DocLib.Instance.GetDocReader(file.Content, new PageDimensions(1080, 1920));
            var page = doc.GetPageReader(pageNumber);
            var data = ConvertToTiffByteArray(page);

            return new InMemoryFile
            {
                FileName = pdfFilename,
                Content = data
            };
        }

        private IList<byte[]> ConvertToListByteArray(IDocReader docReader)
        {
            var listArray = new List<byte[]>();
            var numberOfPages = docReader.GetPageCount();
            for (var i = 0; i < numberOfPages; i++)
            {
                var pageReader = docReader.GetPageReader(i);
                var rawBytes = ConvertToTiffByteArray(pageReader);
                listArray.Add(rawBytes);
            }

            return listArray;
        }

        private byte[] ConvertToTiffByteArray(IPageReader pageReader)
        {
            var rawBytes = pageReader.GetImage(new NaiveTransparencyRemover(255, 255, 255));
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();

            var characters = pageReader.GetCharacters();

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            AddBytes(bmp, rawBytes);
            DrawRectangles(bmp, characters);

            using var stream = new MemoryStream();

            bmp.Save(stream, ImageFormat.Tiff);

            return stream.ConvertToArray();
        }

        private void AddBytes(Bitmap bmp, byte[] rawBytes)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);

            var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
            var pNative = bmpData.Scan0;

            Marshal.Copy(rawBytes, 0, pNative, rawBytes.Length);
            bmp.UnlockBits(bmpData);
        }

        private void DrawRectangles(Bitmap bmp, IEnumerable<Character> characters)
        {
            var pen = new Pen(Color.Red);

            using var graphics = Graphics.FromImage(bmp);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

            foreach (var c in characters)
            {
                var rect = new Rectangle(c.Box.Left, c.Box.Top, c.Box.Right - c.Box.Left, c.Box.Bottom - c.Box.Top);
                graphics.DrawRectangle(pen, rect);
            }
        }
    }
}
