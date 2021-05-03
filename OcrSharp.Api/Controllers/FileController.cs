using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OcrSharp.Api.ViewModel;
using OcrSharp.Domain;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service.Extensions;
using System;
using System.IO;
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
        private readonly ILogger _logger;

        public FileController(IOcrFileService ocrFileService, IDocumentFileService documentService, ILoggerFactory loggerFactory)
        {
            _ocrService = ocrFileService;
            _documentService = documentService;
            _logger = loggerFactory.CreateLogger<FileController>();
        }

        [Consumes("multipart/form-data")]
        [HttpPut("image/for-ocr/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImageForOcr(IFormFile inputFile, Accuracy accuracy = Accuracy.Low, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Validando extenção arquivo");
            if (!Path.GetExtension(inputFile.FileName).ToLower().Equals(".bmp")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".tif")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".tiff")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".jpeg")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".jpg")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".jpe")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".jfif")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".png"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            var inMemory = new InMemoryFile()
            {
                FileName = inputFile.FileName,
                Content = inputFile.OpenReadStream().ConvertToArray(cancellationToken)
            };

            var result = await _ocrService.ApplyOcrAsync(inMemory, accuracy);

            return Ok(Encoding.UTF8.GetString(result.Content));
        }

        [Consumes("multipart/form-data")]
        [HttpPut("image/for-data-ocr/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImageForDataOcr(IFormFile inputFile, Accuracy accuracy = Accuracy.Low, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Validando extenção arquivo");
            if (!Path.GetExtension(inputFile.FileName).ToLower().Equals(".bmp")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".tif")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".tiff")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".jpeg")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".jpg")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".jpe")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".jfif")
             && !Path.GetExtension(inputFile.FileName.ToLower()).Equals(".png"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            var inMemory = new InMemoryFile()
            {
                FileName = inputFile.FileName,
                Content = inputFile.OpenReadStream().ConvertToArray(cancellationToken)
            };

            var result = await _ocrService.ApplyOcrAsync(inMemory, accuracy);
            var ocr = new OcrResultViewModel
            {
                FileName = inputFile.FileName,
                Accuracy = result.Accuracy,
                Content = Encoding.UTF8.GetString(result.Content),
                RunTime = result.RunTime,
            };

            return Ok(ocr);
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/to-tiff")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertPdfFileToImages(IFormFile inputFile, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Validando extenção arquivo");
            if (!Path.GetExtension(inputFile.FileName).ToLower().Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using var st = inputFile.OpenReadStream();
            var archive = await _documentService.ConvertPdfFileToImagesZippedAsync(new InMemoryFile
            {
                FileName = inputFile.FileName,
                Content = st.ConvertToArray(cancellationToken)
            });
            var filename = $"IMAGES_RESULTS_{DateTime.Now.GetDateNowEngFormat()}.zip";
            return ConfigurationFileStreamToDownload(archive, filename, "application/zip");
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/number-of-pages")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult GetNumberOfPages(IFormFile inputFile)
        {
            _logger.LogInformation("Validando extenção arquivo");
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            _logger.LogInformation("Obtendo o número de páginas");
            using var st = inputFile.OpenReadStream();
            var file = new InMemoryFile()
            {
                FileName = Regex.Replace(Regex.Replace(inputFile.FileName.Trim(), @"[-]+", string.Empty, RegexOptions.None), @"[,()\s]+", "_", RegexOptions.CultureInvariant),
                Content = st.ConvertToArray()
            };

            return Ok(_documentService.GetNumberOfPages(file));
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/page-to-tiff/{pageNumber}")]
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
                Content = st.ConvertToArray(cancellationToken)
            };

            _logger.LogInformation($"Convertendo página {page} do arquivo {inMemory.FileName} em imagem");
            var fileImage = _documentService.ConvertPdfPageToImageAsync(inMemory, page);
            _logger.LogInformation($"Conversão finalizada");
            return await Task.FromResult(ConfigurationFileStreamToDownload(fileImage.Content.ToStream(), fileImage.FileName, "image/tiff"));
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/extract-text-by-page/{pageNumber}/accuracy/{accuracy}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExtracTextFromPdfPage(IFormFile inputFile, int pageNumber, Accuracy accuracy = Accuracy.Low, CancellationToken cancellationToken = default)
        {
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using var st = inputFile.OpenReadStream();
            var inMemory = new InMemoryFile()
            {
                FileName = inputFile.FileName,
                Content = st.ConvertToArray(cancellationToken)
            };

            _logger.LogInformation($"Extraindo texto da página {pageNumber} do documento {inMemory.FileName}");
            var page = await _documentService.ExtracTextFromPdfPageAsync(inMemory, pageNumber, accuracy, cancellationToken);
            _logger.LogInformation($"Extração do texto finalizada.");
            var textResult = Regex.Replace(page.Content, @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
            return Ok(textResult);
        }

        [Consumes("multipart/form-data")]
        [HttpPut("pdf/extract-text/accuracy/{accuracy}/connectionId/{connectionId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult ExtractText(IFormFile inputFile, string connectionId, Accuracy accuracy = Accuracy.Low, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!Path.GetExtension(inputFile.FileName).Equals(".pdf"))
                return BadRequest($@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)");

            using var st = inputFile.OpenReadStream();
            var inMemory = new InMemoryFile()
            {
                FileName = inputFile.FileName,
                Content = st.ConvertToArray(cancellationToken),
            };

            Task.Factory.StartNew(async () => await _documentService.ExtractTextFromPdf(connectionId, inMemory, accuracy, cancellationToken));

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