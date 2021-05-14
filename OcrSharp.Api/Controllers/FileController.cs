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
using System.Net.Mime;
using System.Threading.Tasks;

namespace OcrSharp.Api.Controllers
{
    [Route("api/file")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly ITesseractService _tesseractService;
        private readonly IPdfToImageConverter _pdfToImageConverter;
        private readonly IOpenCvService _openCvService;
        private readonly IFileUtilityService _fileUtilityService;
        private readonly ILogger _logger;

        public FileController(ILoggerFactory loggerFactory, IOpenCvService openCvService,
            IFileUtilityService fileUtilityService, ITesseractService tesseractService,
            IPdfToImageConverter pdfToImageConverter)
        {
            _openCvService = openCvService;
            _tesseractService = tesseractService;
            _fileUtilityService = fileUtilityService;
            _pdfToImageConverter = pdfToImageConverter;
            _logger = loggerFactory.CreateLogger<FileController>();
        }

        [Consumes("multipart/form-data")]
        [HttpPut("image/for-ocr/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImageForOcr(IFormFile inputFile, Accuracy accuracy = Accuracy.Low)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());
            try
            {
                _logger.LogInformation("Validando extenção arquivo");
                var extension = Path.GetExtension(inputFile.FileName).ToLower();
                if (!extension.Equals(".bmp")
                 && !extension.Equals(".tif")
                 && !extension.Equals(".tiff")
                 && !extension.Equals(".jpeg")
                 && !extension.Equals(".jpg")
                 && !extension.Equals(".jpe")
                 && !extension.Equals(".jfif")
                 && !extension.Equals(".png"))
                    return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

                await _fileUtilityService.CreateFolder(tempPath);

                var tempInputFile = $"{await _fileUtilityService.NewTempFileName(tempPath)}{extension}";
                var image = await _openCvService.ImageSmootheningAsync(new System.Drawing.Bitmap(inputFile.OpenReadStream()));
                image.Save(tempInputFile);

                var result = await _tesseractService.GetText(tempInputFile, extension, accuracy);
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
        [HttpPut("image/for-data-ocr/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImageForDataOcr(IFormFile inputFile, Accuracy accuracy = Accuracy.Low)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());

            try
            {
                _logger.LogInformation("Validando extenção arquivo");
                var extension = Path.GetExtension(inputFile.FileName).ToLower();
                if (!extension.Equals(".bmp")
                 && !extension.Equals(".tif")
                 && !extension.Equals(".tiff")
                 && !extension.Equals(".jpeg")
                 && !extension.Equals(".jpg")
                 && !extension.Equals(".jpe")
                 && !extension.Equals(".jfif")
                 && !extension.Equals(".png"))
                    return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

                await _fileUtilityService.CreateFolder(tempPath);

                var tempInputFile = $"{await _fileUtilityService.NewTempFileName(tempPath)}{extension}";
                var image = await _openCvService.ImageSmootheningAsync(new System.Drawing.Bitmap(inputFile.OpenReadStream()));
                image.Save(tempInputFile);

                var result = await _tesseractService.GetText(tempInputFile, extension, accuracy);

                TimeSpan el = stopWatch.Elapsed;
                string strElapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                el.Hours, el.Minutes, el.Seconds,
                el.Milliseconds / 10);

                var ocr = new OcrResultViewModel
                {
                    FileName = Path.GetFileName(tempInputFile),
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
        [HttpPut("pdf/to-tiff")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertPdfFileToImages(IFormFile inputFile)
        {
            _logger.LogInformation("Validando extenção arquivo");
            var extension = Path.GetExtension(inputFile.FileName).ToLower();
            if (!extension.Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            const string extensionImage = ".tif";
            var listBuffres = await _pdfToImageConverter.ConvertToStreams(inputFile.OpenReadStream().ConvertToArray(), extensionImage);

            _logger.LogInformation("Zipping files ...");
            var archive = await _fileUtilityService.GetZipArchive(listBuffres, extensionImage);
            var filename = $"IMAGES_RESULTS_{DateTime.Now.GetDateNowEngFormat()}.zip";
            _logger.LogInformation("Files zipped ...");
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
            var extension = Path.GetExtension(inputFile.FileName).ToLower();
            if (!extension.Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            _logger.LogInformation("Obtendo o número de páginas");
            return Ok(await _pdfToImageConverter.GetNumberOfPageAsync(inputFile.OpenReadStream().ConvertToArray()));
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/page-to-tiff/{pageNumber}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertPdfPageToImageAsync(IFormFile inputFile, int pageNumber)
        {
            _logger.LogInformation("Validando extenção arquivo");
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            _logger.LogInformation($"Convertendo página {pageNumber} do arquivo {inputFile.FileName} em imagem");
            using (var inputStream = inputFile.OpenReadStream())
            {
                var image = await _pdfToImageConverter.ConvertPdfPageToImageStream(inputStream.ConvertToArray(), pageNumber, ".tif");
                    _logger.LogInformation($"Conversão finalizada. File Length = {image.Length}");

                var tempInputFile = $"{Guid.NewGuid().ToString("N").ToUpper()}.tif";
                return await Task.FromResult(ConfigurationFileStreamToDownload(image, tempInputFile, "image/tiff"));
            }
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/extract-text-by-page/{pageNumber}/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExtracTextFromPdfPage(IFormFile inputFile, int pageNumber, Accuracy accuracy = Accuracy.Low)
        {
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            _logger.LogInformation("Validando extenção arquivo");
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            _logger.LogInformation($"Convertendo página {pageNumber} do arquivo {inputFile.FileName} em imagem");
            using (var inputStream = inputFile.OpenReadStream())
            {
                using (var imageStream = await _pdfToImageConverter.ConvertPdfPageToImageStream(inputStream.ConvertToArray(), pageNumber, ".tif"))
                {
                    _logger.LogInformation($"Conversão finalizada");
                    var tempInputFile = $"{Guid.NewGuid().ToString("N").ToUpper()}.tif";
                    using (var image = await _openCvService.ImageSmootheningAsync(new System.Drawing.Bitmap(imageStream)))
                    {
                        image.Save(tempInputFile);
                        var result = await _tesseractService.GetText(tempInputFile, ".tif", accuracy);
                        return Ok(result);
                    }
                }
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