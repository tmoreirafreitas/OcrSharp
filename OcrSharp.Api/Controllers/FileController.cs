using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
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
        private readonly IPdfFileService _pdfFileService;

        public FileController(IFileUtilityService fileUtilityService, IOcrFileService ocrFileService, IPdfFileService pdfFileService)
        {
            _fileUtilityService = fileUtilityService;
            _ocrService = ocrFileService;
            _pdfFileService = pdfFileService;
        }

        [HttpPut("image/create-ocr")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ApplyOcrInImageAsync(IFormFileCollection fileCollection, CancellationToken cancellationToken)
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

            foreach (var file in fileCollection)
            {
                var inMemory = new InMemoryFile()
                {
                    FileName = file.FileName,
                    Content = await file.OpenReadStream().StreamToArrayAsync(cancellationToken)
                };
                
                filesToZip.Add(await _ocrService.ApplyOcrAsync(inMemory));
            }

            var zipFile = await _fileUtilityService.GetZipArchive(filesToZip, cancellationToken);
            var filename = $"OCR_RESULT_{DateTime.Now.GetDateNowEngFormat()}.zip";
            return ConfigurationFileStreamToDownload(zipFile, filename, "application/zip");
        }

        [HttpPut("pdf/convert-multiple-to-png")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertMultiplePdfFileToImages(IFormFileCollection fileCollection, CancellationToken cancellationToken)
        {
            var filesNotSuport = fileCollection.Where(x => !Path.GetExtension(x.FileName).Equals(".pdf"));
            if (filesNotSuport.Any())
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            var filesToZip = new List<InMemoryFile>();
            foreach (var file in fileCollection)
            {
                if (cancellationToken.IsCancellationRequested)
                    cancellationToken.ThrowIfCancellationRequested();

                using var st = file.OpenReadStream();
                filesToZip.Add(new InMemoryFile
                {
                    FileName = file.FileName,
                    Content = await st.StreamToArrayAsync(cancellationToken)
                });
            }

            var archive = await _pdfFileService.ConvertMultiplePdfToImageAsync(filesToZip, cancellationToken);
            var filename = $"IMAGES_RESULTS_{DateTime.Now.GetDateNowEngFormat()}.zip";
            return ConfigurationFileStreamToDownload(archive.Content.ArrayToStream(), filename, "application/zip");
        }

        [HttpPut("pdf/convert-page-to-png/{page}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertPdfPageToImageAsync(IFormFile inputFile, int page, CancellationToken cancellationToken)
        {
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using var st = inputFile.OpenReadStream();
            var inMemory = new InMemoryFile()
            {
                FileName = inputFile.FileName,
                Content = await st.StreamToArrayAsync(cancellationToken)
            };

            var fileImage = await _pdfFileService.ConvertPdfPageToImageAsync(inMemory, page, cancellationToken);
            return ConfigurationFileStreamToDownload(fileImage.Content.ArrayToStream(), fileImage.FileName, "image/png");
        }

        [HttpPut("pdf/extract-text-by-page/{pageNumber}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExtracTextFromPdfPage(IFormFile inputFile, int pageNumber, CancellationToken cancellationToken)
        {
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using var st = inputFile.OpenReadStream();
            var inMemory = new InMemoryFile()
            {
                FileName = inputFile.FileName,
                Content = await st.StreamToArrayAsync(cancellationToken)
            };

            var page = await _pdfFileService.ExtracTextFromPdfPageAsync(inMemory, pageNumber, cancellationToken);
            var textResult = Regex.Replace(page.Content, @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
            return Ok(textResult);
        }

        [HttpPut("pdf/extract-text")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExtractText(IFormFile inputFile, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using var st = inputFile.OpenReadStream();
            var inMemory = new InMemoryFile()
            {
                FileName = inputFile.FileName,
                Content = await st.StreamToArrayAsync(cancellationToken)
            };

            var pdf = await _pdfFileService.ExtractTextFromPdf(inMemory, cancellationToken);
            return Ok(pdf);
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