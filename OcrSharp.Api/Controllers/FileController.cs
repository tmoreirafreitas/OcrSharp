using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace OcrSharp.Api.Controllers
{

    [Route("api/file")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly string _folderToUpload;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IFileUtilityService _fileUtilityService;
        private readonly IOcrFileService _ocrService;
        private readonly IPdfFileService _pdfFileService;

        public FileController(IConfiguration configuration, IWebHostEnvironment hostingEnvironment,
         IFileUtilityService fileUtilityService, IOcrFileService ocrFileService, IPdfFileService pdfFileService)
        {
            _fileUtilityService = fileUtilityService;
            _ocrService = ocrFileService;
            _pdfFileService = pdfFileService;
            _folderToUpload = configuration["FolderToUpload:Name"] ?? "TempFile";
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpPost("image/create-ocr")]
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
            var tempFolder = Path.Combine(_hostingEnvironment.WebRootPath, _folderToUpload, Guid.NewGuid().ToString());

            foreach (var file in fileCollection)
            {
                var fileName = file.FileName;
                var stream = file.OpenReadStream();

                var pathFile = Path.Combine(tempFolder, fileName);
                await _fileUtilityService.CreateFolder(tempFolder, cancellationToken);
                await _fileUtilityService.CreateFileAsync(stream, pathFile, cancellationToken);
                filesToZip.Add(await _ocrService.ApplyOcrAsync(pathFile));
            }

            await _fileUtilityService.DeleteAllAsync(tempFolder, true, cancellationToken);
            var zipFile = await _fileUtilityService.GetZipArchive(filesToZip, cancellationToken);
            var filename = $"OCR_RESULT_{DateTime.Now.GetDateNowEngFormat()}.zip";
            return ConfigurationFileStreamToDownload(zipFile, filename, "application/zip");
        }

        [HttpPost("pdf/convert-multiple-to-png")]
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

        [HttpPost("pdf/convert-page-to-png/{page}")]
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

        [HttpPost("pdf/extract-text-by-page/{pageNumber}")]
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
            var textResult = Regex.Replace(page.Text, @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
            return Ok(textResult);
        }

        [HttpPost("image/extract-table")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TextDetectionAndRecognitionToConvertTables(IFormFile inputFile, CancellationToken cancellationToken)
        {
            var ext = Path.GetExtension(inputFile.FileName);
            switch (ext)
            {
                case ".bmp":
                case ".tif":
                case ".tiff":
                case ".jpeg":
                case ".jpg":
                case ".jpe":
                case ".jfif":
                case ".png":
                    var filesToZip = new List<InMemoryFile>();
                    var tempFolder = Path.Combine(_hostingEnvironment.WebRootPath, _folderToUpload, Guid.NewGuid().ToString());

                    var fileName = inputFile.FileName;
                    using (var stream = inputFile.OpenReadStream())
                    {
                        var pathFile = Path.Combine(tempFolder, fileName);
                        await _fileUtilityService.CreateFolder(tempFolder, cancellationToken);
                        await _fileUtilityService.CreateFileAsync(await stream.StreamToArrayAsync(), pathFile, cancellationToken);
                        filesToZip.Add(await _ocrService.TextDetectionAndRecognitionToConvertTables(pathFile));
                    }                    

                    await _fileUtilityService.DeleteAllAsync(tempFolder, true, cancellationToken);
                    var zipFile = await _fileUtilityService.GetZipArchive(filesToZip, cancellationToken);
                    var filename = $"OCR_RESULT_{DateTime.Now.GetDateNowEngFormat()}.zip";
                    return ConfigurationFileStreamToDownload(zipFile, filename, "application/zip");

                default:
                    return BadRequest($@"Há extensão de arquivo não suportado, os tipos suportados são: 
                                    Bitmap(*.bmp), JPEG(*.jpeg; *.jpg; *.jpe; *.jfif), TIFF(*.tif; *.tiff) e PNG(*.png)");
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