using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OcrSharp.Api.ViewModel;
using OcrSharp.Domain;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service.Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;

namespace OcrSharp.Api.Controllers
{
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/ocr")]
    public class OcrController : ControllerBase
    {
        private readonly ITesseractService _tesseractService;
        private readonly IPdfToImageConverter _pdfToImageConverter;
        private readonly IOpenCvService _openCvService;
        private readonly IFileUtilityService _fileUtilityService;
        private readonly ILogger _logger;
        private string[] FileExtensions => new string[] { ".tif", ".tiff", ".jpeg", ".jpg", ".jpe", ".jfif", ".png" };

        public OcrController(ILoggerFactory loggerFactory, IOpenCvService openCvService,
            IFileUtilityService fileUtilityService, ITesseractService tesseractService,
            IPdfToImageConverter pdfToImageConverter)
        {
            _openCvService = openCvService;
            _tesseractService = tesseractService;
            _fileUtilityService = fileUtilityService;
            _pdfToImageConverter = pdfToImageConverter;
            _logger = loggerFactory.CreateLogger<OcrController>();
        }

        [Consumes("multipart/form-data")]
        [HttpPost("to-file/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImageForOcr(IFormFile file, Accuracy accuracy = Accuracy.Low)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());
            try
            {
                _logger.LogInformation("Validating file extension.");
                var extension = Path.GetExtension(file.FileName).ToLower();
                if (!FileExtensions.Contains(extension))
                    return BadRequest($@"There is an unsupported file extension, the supported type is: JPG, PNG, TIFF");

                await _fileUtilityService.CreateFolder(tempPath);

                var tempfile = $"{await _fileUtilityService.NewTempFileName(tempPath)}{extension}";
                var image = await _openCvService.ImageSmootheningAsync(new System.Drawing.Bitmap(file.OpenReadStream()));
                image.Save(tempfile);

                var result = await _tesseractService.GetText(tempfile, extension, accuracy);
                return Ok(result);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                await _fileUtilityService.DeleteAllAsync(tempPath, true);
            }
        }

        [Consumes("multipart/form-data")]
        [HttpPost("to-data/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImageForDataOcr(IFormFile file, Accuracy accuracy = Accuracy.Low)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());

            try
            {
                _logger.LogInformation("Validating file extension.");
                var extension = Path.GetExtension(file.FileName).ToLower();
                if (!FileExtensions.Contains(extension))
                    return BadRequest($@"There is an unsupported file extension, the supported type is: JPG, PNG, TIFF");

                await _fileUtilityService.CreateFolder(tempPath);

                var tempfile = $"{await _fileUtilityService.NewTempFileName(tempPath)}{extension}";
                var image = await _openCvService.ImageSmootheningAsync(new System.Drawing.Bitmap(file.OpenReadStream()));
                image.Save(tempfile);

                var result = await _tesseractService.GetText(tempfile, extension, accuracy);

                TimeSpan el = stopWatch.Elapsed;
                string strElapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                el.Hours, el.Minutes, el.Seconds,
                el.Milliseconds / 10);

                var ocr = new OcrResultViewModel
                {
                    FileName = Path.GetFileName(tempfile),
                    Content = result,
                    RunTime = strElapsedTime,
                };

                return Ok(ocr);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                await _fileUtilityService.DeleteAllAsync(tempPath, true);
            }
        }

        [Consumes("multipart/form-data")]
        [HttpPost("pdf/extract-text-by-page/{pageNumber}/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExtracTextFromPdfPage(IFormFile file, int pageNumber, Accuracy accuracy = Accuracy.Low)
        {
            if (!Path.GetExtension(file.FileName).Equals(".pdf"))
                return BadRequest($@"There is an unsupported file extension, the supported type is: Adobe PDF files (*. Pdf)");

            _logger.LogInformation("Validating file extension.");
            if (!Path.GetExtension(file.FileName).Equals(".pdf"))
                return BadRequest($@"There is an unsupported file extension, the supported type is: Adobe PDF files (*. Pdf)");
            
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());

            try
            {
                using (var inputStream = file.OpenReadStream())
                {
                    _logger.LogInformation($"Converting page {pageNumber} of the file {file.FileName} in image");
                    using (var imageStream = await _pdfToImageConverter.ConvertPdfPageToImageStream(inputStream.ConvertToArray(), pageNumber, ".tif"))
                    {
                        _logger.LogInformation($"Conversion finished.");

                        await _fileUtilityService.CreateFolder(tempPath);
                        var tempfile = $"{await _fileUtilityService.NewTempFileName(tempPath)}.tif";

                        using (var image = await _openCvService.ImageSmootheningAsync(new System.Drawing.Bitmap(imageStream)))
                        {
                            image.Save(tempfile);
                            var result = await _tesseractService.GetText(tempfile, ".tif", accuracy);
                            return Ok(result);
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                await _fileUtilityService.DeleteAllAsync(tempPath, true);
            }
        }
    }
}