using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OcrSharp.Api.Hubs;
using OcrSharp.Api.ViewModel;
using OcrSharp.Domain;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OcrSharp.Api.Controllers
{
    [Route("api/file")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly IOcrFileService _ocrService;
        private readonly IDocumentFileService _documentService;
        private readonly IHubContext<StreamingHub> _streaming;
        private readonly ILogger _logger;

        public FileController(IOcrFileService ocrFileService, IDocumentFileService documentService, ILoggerFactory loggerFactory,
            IHubContext<StreamingHub> streamingHub)
        {
            _ocrService = ocrFileService;
            _documentService = documentService;
            _logger = loggerFactory.CreateLogger<FileController>();
            _streaming = streamingHub;
        }

        [Consumes("multipart/form-data")]
        [HttpPut("image/send-multiple-images-for-ocr/accuracy/{accuracy}/connectionId/{connectionId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendMultipleImagesForOcr(IFormFileCollection fileCollection, string connectionId, Accuracy accuracy = Accuracy.Medium,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Validando extenção dos arquivos de imagens");
            var filesNotSuport = fileCollection.Where(x => !Path.GetExtension(x.FileName.ToLower()).Equals(".bmp")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".tif")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".tiff")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".jpeg")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".jpg")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".jpe")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".jfif")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".png"));

            if (filesNotSuport.Any())
                return BadRequest($@"Há extensão de arquivo não suportado, os tipos suportados são: 
                                    Bitmap(*.bmp), JPEG(*.jpeg; *.jpg; *.jpe; *.jfif), TIFF(*.tif; *.tiff) e PNG(*.png)");

            var index = 0;
            var totalFiles = fileCollection.Count;
            await fileCollection.ParallelForEachAsync(async file =>
            {
                var inMemory = new InMemoryFile()
                {
                    FileName = file.FileName,
                    Page = ++index,
                    Content = await file.OpenReadStream().StreamToArrayAsync(cancellationToken)
                };

                var pageResult = await _ocrService.ApplyOcrAsync(inMemory, accuracy);
                var ocr = new OcrResultViewModel
                {
                    FileName = inMemory.FileName,
                    Accuracy = pageResult.Accuracy,
                    ContentType = "text/plain",
                    Content = Convert.ToBase64String(pageResult.Content, Base64FormattingOptions.InsertLineBreaks),
                    CurrentPage = inMemory.Page,
                    NumberOfPages = totalFiles,
                    RunTime = pageResult.RunTime,
                    IsBase64 = true
                };
                string jsonData = string.Format("{0}\n", JsonConvert.SerializeObject(ocr));
                await _streaming.Clients.Client(connectionId).SendAsync("OcrResultData", jsonData, cancellationToken);

            }, Environment.ProcessorCount);

            return Ok("Imagens enviada com sucesso para extração de textos.");
        }

        [Consumes("multipart/form-data")]
        [HttpPut("image/send-multiple-images-for-data-ocr/accuracy/{accuracy}/connectionId/{connectionId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendMultipleImagesForDataOcr(IFormFileCollection fileCollection, string connectionId,
            Accuracy accuracy = Accuracy.Medium, CancellationToken cancellationToken = default)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            _logger.LogInformation("Validando extenção dos arquivos de imagens");
            var filesNotSuport = fileCollection.Where(x => !Path.GetExtension(x.FileName.ToLower()).Equals(".bmp")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".tif")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".tiff")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".jpeg")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".jpg")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".jpe")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".jfif")
            && !Path.GetExtension(x.FileName.ToLower()).Equals(".png"));

            if (filesNotSuport.Any())
                return BadRequest($@"Há extensão de arquivo não suportado, os tipos suportados são: 
                                    Bitmap(*.bmp), JPEG(*.jpeg; *.jpg; *.jpe; *.jfif), TIFF(*.tif; *.tiff) e PNG(*.png)");

            var doc = new DocumentFile(1, "");
            var index = 0;
            var totalFiles = fileCollection.Count;
            var currentPage = 0;
            await fileCollection.ParallelForEachAsync(async file =>
            {
                var inMemory = new InMemoryFile()
                {
                    FileName = file.FileName,
                    Page = ++index,
                    Content = file.OpenReadStream().StreamToArrayAsync(cancellationToken).Result
                };

                var result = await _ocrService.ApplyOcrAsync(inMemory, accuracy);
                var ocr = new OcrResultViewModel
                {
                    FileName = file.FileName,
                    Accuracy = result.Accuracy,
                    Content = Encoding.UTF8.GetString(result.Content),
                    NumberOfPages = totalFiles,
                    RunTime = result.RunTime,
                    CurrentPage = ++currentPage,
                    IsBase64 = false
                };
                string jsonData = string.Format("{0}\n", JsonConvert.SerializeObject(ocr));
                await _streaming.Clients.Client(connectionId).SendAsync("OcrResultData", jsonData, cancellationToken);
            }, Environment.ProcessorCount);

            return Ok("Imagens enviada com sucesso para extração de textos.");
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/convert-multiple-to-tiff")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertMultiplePdfFileToImages(IFormFileCollection fileCollection, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Validando extenção arquivo");
            var filesNotSuport = fileCollection.Where(x => !Path.GetExtension(x.FileName).Equals(".pdf"));
            if (filesNotSuport.Any())
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            var filesToZip = new List<InMemoryFile>();

            object locker = new object();
            await fileCollection.ParallelForEachAsync(async file =>
            {
                await Task.Run(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        cancellationToken.ThrowIfCancellationRequested();

                    using var st = file.OpenReadStream();
                    lock (locker)
                        filesToZip.Add(new InMemoryFile
                        {
                            FileName = file.FileName,
                            Content = st.StreamToArrayAsync(cancellationToken).Result
                        });
                });
            }, Environment.ProcessorCount);

            var tmp = filesToZip.AsEnumerable();
            var archive = _documentService.ConvertMultiplePdfToImage(ref tmp, cancellationToken);
            var filename = $"IMAGES_RESULTS_{DateTime.Now.GetDateNowEngFormat()}.zip";
            return await Task.FromResult(ConfigurationFileStreamToDownload(archive.Content.ArrayToStream(), filename, "application/zip"));
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/convert-to-tiff")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertPdfFileToImages(IFormFile inputFile, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Validando extenção arquivo");
            if (!Path.GetExtension(inputFile.FileName).ToLower().Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using var st = inputFile.OpenReadStream();
            var archive = _documentService.ConvertPdfFileToImages(new InMemoryFile
            {
                FileName = inputFile.FileName,
                Content = await st.StreamToArrayAsync(cancellationToken)
            });
            var filename = $"IMAGES_RESULTS_{DateTime.Now.GetDateNowEngFormat()}.zip";
            return ConfigurationFileStreamToDownload(archive, filename, "application/zip");
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/number-of-pages")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNumberOfPages(IFormFile inputFile)
        {
            _logger.LogInformation("Validando extenção arquivo");
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            _logger.LogInformation("Obtendo o número de páginas");
            using var st = inputFile.OpenReadStream();
            var file = new InMemoryFile()
            {
                FileName = Regex.Replace(Regex.Replace(inputFile.FileName.Trim(), @"[-]+", string.Empty, RegexOptions.None), @"[,()\s]+", "_", RegexOptions.CultureInvariant),
                Content = await st.StreamToArrayAsync()
            };

            return Ok(_documentService.GetNumberOfPages(file));
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/convert-page-to-tiff/{page}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertPdfPageToImageAsync(IFormFile inputFile, int page, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Validando extenção arquivo");
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using var st = inputFile.OpenReadStream();
            var inMemory = new InMemoryFile()
            {
                FileName = Regex.Replace(Regex.Replace(inputFile.FileName.Trim(), @"[-]+", string.Empty, RegexOptions.None), @"[,()\s]+", "_", RegexOptions.CultureInvariant),
                Content = await st.StreamToArrayAsync(cancellationToken)
            };

            _logger.LogInformation($"Convertendo página {page} do arquivo {inMemory.FileName} em imagem");
            var fileImage = _documentService.ConvertPdfPageToImage(inMemory, page);
            _logger.LogInformation($"Conversão finalizada");
            return ConfigurationFileStreamToDownload(fileImage.Content.ArrayToStream(), fileImage.FileName, "image/tiff");
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/extract-text-by-page/{pageNumber}/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExtracTextFromPdfPage(IFormFile inputFile, int pageNumber, Accuracy accuracy = Accuracy.Medium, CancellationToken cancellationToken = default)
        {
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using var st = inputFile.OpenReadStream();
            var inMemory = new InMemoryFile()
            {
                FileName = inputFile.FileName,
                Content = await st.StreamToArrayAsync(cancellationToken)
            };

            _logger.LogInformation($"Extraindo texto da página {pageNumber} do documento {inMemory.FileName}");
            var page = await _documentService.ExtracTextFromPdfPageAsync(inMemory, pageNumber, accuracy, cancellationToken);
            _logger.LogInformation($"Extração do texto finalizada.");
            var textResult = Regex.Replace(page.Content, @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
            return Ok(textResult);
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/send-file-to-extract-text/accuracy/{accuracy}/connectionId/{connectionId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult ExtractText(IFormFile inputFile, string connectionId, Accuracy accuracy = Accuracy.Medium, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using var st = inputFile.OpenReadStream();
            var inMemory = new InMemoryFile()
            {
                FileName = inputFile.FileName,
                Content = st.StreamToArrayAsync(cancellationToken).GetAwaiter().GetResult()
            };

            var numberOfPages = _documentService.GetNumberOfPages(inMemory);
            Task.Factory.StartNew(() =>
            {
                //var maxthreads = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 2.0));
                var maxthreads = Environment.ProcessorCount;
                var pages = _documentService.GetPages(inMemory);
                var index = 0;
                pages.ParallelForEachAsync(async page =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await _documentService.ExtracTextFromPdfPageAsync(inMemory, ++index, accuracy);
                    var ocr = new OcrResultViewModel
                    {
                        FileName = inMemory.FileName,
                        Accuracy = result.Accuracy,
                        CurrentPage = result.PageNumber,
                        Content = result.Content,
                        NumberOfPages = numberOfPages,
                        RunTime = result.RunTime,
                        IsBase64 = false
                    };
                    string jsonData = string.Format("{0}\n", JsonConvert.SerializeObject(ocr));
                    await _streaming.Clients.Client(connectionId).SendAsync("OcrResultData", jsonData, cancellationToken);
                }, maxthreads);
            });

            return Ok("Arquivo enviado para extração dos textos, por favor aguarde um momento");
        }

        private FileStreamResult ConfigurationFileStreamToDownload(Stream file, string fileName, string contentType)
        {
            HttpContext.Response.Headers.Add("Content-Type", contentType);
            HttpContext.Response.Headers.Add("Content-Filename", fileName);
            HttpContext.Response.Headers.Add("content-disposition", $"attachment;filename={fileName}");
            HttpContext.Response.Headers.Add("Access-Control-Expose-Headers", "Content-Filename");
            return File(file, contentType);
        }
    }
}