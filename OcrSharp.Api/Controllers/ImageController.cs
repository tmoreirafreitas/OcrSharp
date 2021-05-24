using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service.Extensions;
using System;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

namespace OcrSharp.Api.Controllers
{
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/image")]
    public class ImageController : ControllerBase
    {
        private readonly IPdfToImageConverter _pdfToImageConverter;
        private readonly IFileUtilityService _fileUtilityService;
        private readonly ILogger _logger;

        public ImageController(IPdfToImageConverter pdfToImageConverter, IFileUtilityService fileUtilityService, ILogger logger)
        {
            _pdfToImageConverter = pdfToImageConverter;
            _fileUtilityService = fileUtilityService;
            _logger = logger;
        }

        [Consumes("multipart/form-data")]
        [HttpPost("pdf/to-tiff")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertPdfFileToImages(IFormFile file)
        {
            _logger.LogInformation("Validating file extension.");
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!extension.Equals(".pdf"))
                return BadRequest($@"There is an unsupported file extension, the supported type is: Adobe PDF files (*. Pdf)");

            const string extensionImage = ".tif";
            var listBuffres = await _pdfToImageConverter.ConvertToStreams(file.OpenReadStream().ConvertToArray(), extensionImage);

            _logger.LogInformation("Zipping files ...");
            var archive = await _fileUtilityService.GetZipArchive(listBuffres, extensionImage);
            var filename = $"{Guid.NewGuid().ToString("N").ToUpper()}.zip";
            _logger.LogInformation("Files zipped ...");
            return ConfigurationFileStreamToDownload(archive, filename, "application/zip");
        }

        [Consumes("multipart/form-data")]
        [HttpPost("pdf/number-of-pages")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNumberOfPages(IFormFile file)
        {
            _logger.LogInformation("Validating file extension.");
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!extension.Equals(".pdf"))
                return BadRequest($@"There is an unsupported file extension, the supported type is: Adobe PDF files (*. Pdf)");

            _logger.LogInformation("Getting the number of pages.");
            return Ok(await _pdfToImageConverter.GetNumberOfPageAsync(file.OpenReadStream().ConvertToArray()));
        }

        [Consumes("multipart/form-data")]
        [HttpPost("pdf/page-to-tiff/{pageNumber}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertPdfPageToImageAsync(IFormFile file, int pageNumber)
        {
            _logger.LogInformation("Validating file extension.");
            if (!Path.GetExtension(file.FileName).Equals(".pdf"))
                return BadRequest($@"There is an unsupported file extension, the supported type is: Adobe PDF files (*. Pdf)");

            _logger.LogInformation($"Converting page {pageNumber} of the file {file.FileName} in image");
            using (var inputStream = file.OpenReadStream())
            {
                var image = await _pdfToImageConverter.ConvertPdfPageToImageStream(inputStream.ConvertToArray(), pageNumber, ".tif");
                _logger.LogInformation($"Conversion finished.");

                var tempfile = $"{Guid.NewGuid().ToString("N").ToUpper()}.tif";
                return await Task.FromResult(ConfigurationFileStreamToDownload(image, tempfile, "image/tiff"));
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
