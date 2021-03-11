using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OcrSharp.Domain;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service.Extensions;
using System;
using System.Collections.Concurrent;
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
        private readonly IFileUtilityService _fileUtilityService;
        private readonly IOcrFileService _ocrService;
        private readonly IDocumentFileService _pdfFileService;

        public FileController(IFileUtilityService fileUtilityService, IOcrFileService ocrFileService, IDocumentFileService pdfFileService)
        {
            _fileUtilityService = fileUtilityService;
            _ocrService = ocrFileService;
            _pdfFileService = pdfFileService;
        }

        [HttpPut("image/get-zipped-file-ocr-of-multiple-images/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetZippedFileOcrOfMultipleImages(IFormFileCollection fileCollection, Accuracy accuracy = Accuracy.Medium, CancellationToken cancellationToken = default)
        {
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

            var filesToZip = new List<InMemoryFile>();
            object listlock = new object();
            Parallel.ForEach(fileCollection, new ParallelOptions
            {
                // multiply the count because a processor can have 2 cores
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            file =>
            {
                var inMemory = new InMemoryFile()
                {
                    FileName = file.FileName,
                    Content = file.OpenReadStream().StreamToArrayAsync(cancellationToken).Result
                };

                lock (listlock)
                    filesToZip.Add(_ocrService.ApplyOcrAsync(inMemory, accuracy).Result);
            });

            var zipFile = await _fileUtilityService.GetZipArchive(filesToZip, cancellationToken);
            var filename = $"OCR_RESULT_{DateTime.Now.GetDateNowEngFormat()}.zip";
            var result = ConfigurationFileStreamToDownload(zipFile, filename, "application/zip");

            return result;
        }

        [HttpPut("image/get-data-ocr-of-multiple-images/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDataOcrOfMultipleImages(IFormFileCollection fileCollection, Accuracy accuracy = Accuracy.Medium, CancellationToken cancellationToken = default)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

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
            object listlock = new object();
            Parallel.ForEach(fileCollection, new ParallelOptions
            {
                // multiply the count because a processor can have 2 cores
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            file =>
            {
                var inMemory = new InMemoryFile()
                {
                    FileName = file.FileName,
                    Content = file.OpenReadStream().StreamToArrayAsync(cancellationToken).Result
                };

                //var doc = new DocumentFile(1, file.FileName);
                var result = _ocrService.ApplyOcrAsync(inMemory, accuracy).Result;
                var page = new DocumentPage(++index, Encoding.UTF8.GetString(result.Content))
                {
                    Accuracy = result.Accuracy,
                    RunTime = result.RunTime
                };
                lock (listlock)
                    doc.Pages.Add(page);
            });

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
            doc.RunTimeTotal = elapsedTime;

            return Ok(await Task.FromResult(doc));
        }

        [HttpPut("pdf/convert-multiple-to-tiff")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertMultiplePdfFileToImages(IFormFileCollection fileCollection, CancellationToken cancellationToken)
        {
            var filesNotSuport = fileCollection.Where(x => !Path.GetExtension(x.FileName).Equals(".pdf"));
            if (filesNotSuport.Any())
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            IEnumerable<InMemoryFile> filesToZip = new List<InMemoryFile>();
            object listlock = new object();
            Parallel.ForEach(fileCollection, new ParallelOptions
            {
                // multiply the count because a processor can have 2 cores
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            file =>
            {
                if (cancellationToken.IsCancellationRequested)
                    cancellationToken.ThrowIfCancellationRequested();

                using (var st = file.OpenReadStream())
                {
                    lock (listlock)
                        ((IList<InMemoryFile>)filesToZip).Add(new InMemoryFile
                        {
                            FileName = file.FileName,
                            Content = st.StreamToArrayAsync(cancellationToken).Result
                        });
                }
            });

            var archive = _pdfFileService.ConvertMultiplePdfToImage(ref filesToZip, cancellationToken);
            var filename = $"IMAGES_RESULTS_{DateTime.Now.GetDateNowEngFormat()}.zip";
            return await Task.FromResult(ConfigurationFileStreamToDownload(archive.Content.ArrayToStream(), filename, "application/zip"));
        }

        [HttpPut("pdf/convert-to-tiff")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertPdfFileToImages(IFormFile inputFile, CancellationToken cancellationToken)
        {
            if (!Path.GetExtension(inputFile.FileName).ToLower().Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using (var st = inputFile.OpenReadStream())
            {
                var archive = _pdfFileService.ConvertPdfFileToImages(new InMemoryFile
                {
                    FileName = inputFile.FileName,
                    Content = await st.StreamToArrayAsync(cancellationToken)
                });
                var filename = $"IMAGES_RESULTS_{DateTime.Now.GetDateNowEngFormat()}.zip";
                return ConfigurationFileStreamToDownload(archive, filename, "application/zip");
            }
        }

        [HttpPut("pdf/convert-page-to-tiff/{page}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertPdfPageToImageAsync(IFormFile inputFile, int page, CancellationToken cancellationToken)
        {
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using (var st = inputFile.OpenReadStream())
            {
                var inMemory = new InMemoryFile()
                {
                    FileName = Regex.Replace(Regex.Replace(inputFile.FileName.Trim(), @"[-]+", string.Empty, RegexOptions.None), @"[,()\s]+", "_", RegexOptions.CultureInvariant),
                    Content = await st.StreamToArrayAsync(cancellationToken)
                };

                var fileImage = _pdfFileService.ConvertPdfPageToImage(inMemory, page);
                return ConfigurationFileStreamToDownload(fileImage.Content.ArrayToStream(), fileImage.FileName, "image/png");
            }
        }

        [HttpPut("pdf/extract-text-by-page/{pageNumber}/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExtracTextFromPdfPage(IFormFile inputFile, int pageNumber, Accuracy accuracy = Accuracy.Medium, CancellationToken cancellationToken = default)
        {
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using (var st = inputFile.OpenReadStream())
            {
                var inMemory = new InMemoryFile()
                {
                    FileName = inputFile.FileName,
                    Content = await st.StreamToArrayAsync(cancellationToken)
                };

                var page = await _pdfFileService.ExtracTextFromPdfPageAsync(inMemory, pageNumber, accuracy, cancellationToken);
                var textResult = Regex.Replace(page.Content, @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
                return Ok(textResult);
            }
        }

        [HttpPut("pdf/extract-text/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExtractText(IFormFile inputFile, Accuracy accuracy = Accuracy.Medium, CancellationToken cancellationToken = default(CancellationToken))
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using (var st = inputFile.OpenReadStream())
            {
                var inMemory = new InMemoryFile()
                {
                    FileName = inputFile.FileName,
                    Content = await st.StreamToArrayAsync(cancellationToken)
                };

                var pdf = await _pdfFileService.ExtractTextFromPdf(inMemory, accuracy, cancellationToken);


                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
                pdf.RunTimeTotal = elapsedTime;

                return Ok(pdf);
            }
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