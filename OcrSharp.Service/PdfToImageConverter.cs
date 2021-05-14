using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OcrSharp.Service
{
    /// <summary>
    /// Service to convert pdf file to image using the NetVips engine
    /// </summary>
    public class PdfToImageConverter : IPdfToImageConverter
    {
        private readonly IConfiguration _configuration;
        private readonly IFileUtilityService _fileUtilityService;
        private readonly IOpenCvService _openCvService;
        private readonly ILogger _logger;
        private int _maxthreads = Convert.ToInt32(Math.Ceiling(Environment.ProcessorCount * 0.8 * 1));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="fileUtilityService"></param>
        /// <param name="openCvService"></param>
        public PdfToImageConverter(IConfiguration configuration, ILoggerFactory loggerFactory,
            IFileUtilityService fileUtilityService, IOpenCvService openCvService)
        {
            _openCvService = openCvService;
            _logger = loggerFactory.CreateLogger<PdfToImageConverter>();
            _configuration = configuration;
            _fileUtilityService = fileUtilityService;

            if (NetVips.ModuleInitializer.VipsInitialized)
            {
                _logger.LogInformation($"Inited libvips {NetVips.NetVips.Version(0)}.{NetVips.NetVips.Version(1)}.{NetVips.NetVips.Version(2)}");
            }
            else
            {
                _logger.LogInformation(NetVips.ModuleInitializer.Exception.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdfDocument"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public async Task<IEnumerable<Stream>> ConvertToStreams(byte[] pdfDocument, string extension)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());
            var list = new List<Stream>();
            try
            {
                await _fileUtilityService.CreateFolder(tempPath);

                var tempInputFile = $"{await _fileUtilityService.NewTempFileName(tempPath)}.pdf";

                await _fileUtilityService.CreateFileAsync(pdfDocument, tempInputFile);

                var imageBytes = File.ReadAllBytes(tempInputFile);

                using (var pdfImage = NetVips.Image.NewFromBuffer(imageBytes))
                {
                    var pageCount = (int)pdfImage.Get("n-pages");
                    var digits = pageCount.ToString().Length;
                    var pages = Enumerable.Range(0, pageCount);
                    var locker = new object();
                    await pages.ParallelForEachAsync(async page =>
                    {
                        using (var image = NetVips.Image.PdfloadBuffer(imageBytes, page: page, n: 1,
                                dpi: Convert.ToInt32(_configuration["Application:Tesseract:Dpi"].Split(" ")[1])))
                        {
                            var filename = Path.Combine(tempPath, $"{Path.GetFileNameWithoutExtension(tempInputFile)}-{page.ToString($"D{digits}")}{extension}");
                            _logger.LogInformation($"Rendering {filename} ...");

                            image.WriteToFile(filename);

                            using (var st = File.OpenRead(filename))
                            {
                                Stream ms = new MemoryStream();
                                await st.CopyToAsync(ms);
                                ms.Position = 0;
                                lock (locker)
                                {
                                    list.Add(ms);
                                    _logger.LogInformation($"Rendered {filename} ...");
                                }
                            }
                        }
                    }, _maxthreads);

                    return list;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdfDocument"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public async Task<IEnumerable<byte[]>> ConvertToBuffers(byte[] pdfDocument, string extension)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());
            var list = new List<byte[]>();
            try
            {
                await _fileUtilityService.CreateFolder(tempPath);
                var tempInputFile = $"{await _fileUtilityService.NewTempFileName(tempPath)}.pdf";

                await _fileUtilityService.CreateFileAsync(pdfDocument, tempInputFile);

                var imageBytes = File.ReadAllBytes(tempInputFile);

                using (var pdfImage = NetVips.Image.NewFromBuffer(imageBytes))
                {
                    var pageCount = (int)pdfImage.Get("n-pages");
                    var digits = pageCount.ToString().Length;
                    var pages = Enumerable.Range(0, pageCount);
                    foreach (var page in pages)
                    {
                        using (var image = NetVips.Image.PdfloadBuffer(imageBytes, page: page, n: 1,
                                dpi: Convert.ToInt32(_configuration["Application:Tesseract:Dpi"].Split(" ")[1])))
                        {
                            var filename = Path.Combine(tempPath, $"{Path.GetFileNameWithoutExtension(tempInputFile)}-{page.ToString($"D{digits}")}.{extension}");
                            _logger.LogInformation($"Rendering {filename} ...");

                            image.WriteToFile(filename);

                            using (var st = File.OpenRead(filename))
                            {
                                using (var ms = new MemoryStream())
                                {
                                    await st.CopyToAsync(ms);
                                    list.Add(ms.ToArray());
                                    _logger.LogInformation($"Rendered {filename} ...");
                                }
                            }
                        }
                    }

                    return list;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdfDocument"></param>
        /// <param name="page"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public async Task<Stream> ConvertPdfPageToImageStream(byte[] pdfDocument, int page, string extension)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());
            try
            {
                await _fileUtilityService.CreateFolder(tempPath);
                var tempInputFile = $"{await _fileUtilityService.NewTempFileName(tempPath)}.pdf";

                await _fileUtilityService.CreateFileAsync(pdfDocument, tempInputFile);

                var imageBytes = File.ReadAllBytes(tempInputFile);

                using (var pdfImage = NetVips.Image.NewFromBuffer(imageBytes))
                {
                    var pageCount = (int)pdfImage.Get("n-pages");
                    var digits = pageCount.ToString().Length;
                    using (var image = NetVips.Image.PdfloadBuffer(imageBytes, page: page, n: 1,
                                dpi: Convert.ToInt32(_configuration["Application:Tesseract:Dpi"].Split(" ")[1])))
                    {
                        var filename = Path.Combine(tempPath, $"{Path.GetFileNameWithoutExtension(tempInputFile)}-{page.ToString($"D{digits}")}{extension}");
                        _logger.LogInformation($"Rendering {filename} ...");

                        image.WriteToFile(filename);

                        using (var st = File.OpenRead(filename))
                        {
                            var ms = new MemoryStream();
                            await st.CopyToAsync(ms);
                            ms.Position = 0;
                            _logger.LogInformation($"Rendered {filename} ...");
                            return ms;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdfDocument"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public async Task<string> ConvertToFiles(byte[] pdfDocument, string extension)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());
            int _maxthreads = Convert.ToInt32(Math.Ceiling(Environment.ProcessorCount * 0.8 * 1));
            await _fileUtilityService.CreateFolder(tempPath);

            var tempInputFile = $"{await _fileUtilityService.NewTempFileName(tempPath)}.pdf";
            await _fileUtilityService.CreateFileAsync(pdfDocument, tempInputFile);

            var pageCount = 0;
            var digits = 0;
            var imageBytes = File.ReadAllBytes(tempInputFile);

            using (var image = NetVips.Image.NewFromBuffer(imageBytes))
            {
                pageCount = (int)image.Get("n-pages");
                digits = pageCount.ToString().Length;

                var pages = Enumerable.Range(0, pageCount);
                await pages.ParallelForEachAsync(currentPage =>
                {
                    using (var page = NetVips.Image.PdfloadBuffer(imageBytes, page: currentPage, n: 1,
                        dpi: Convert.ToInt32(_configuration["Application:Tesseract:Dpi"].Split(" ")[1])))
                    {
                        var filename = Path.Combine(tempPath, $"{Path.GetFileNameWithoutExtension(tempInputFile)}-{currentPage.ToString($"D{digits}")}{extension}");
                        _logger.LogInformation($"Rendering {filename} ...");

                        page.WriteToFile(filename);
                        _logger.LogInformation($"Rendered {filename} ...");

                        return Task.CompletedTask;
                    }
                }, _maxthreads);

                pages = null;
            }
            imageBytes = null;

            return tempPath;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdfDocument"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public async Task<string> ConvertToFilesPreProcessed(byte[] pdfDocument, string extension)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());
            int _maxthreads = Convert.ToInt32(Math.Ceiling(Environment.ProcessorCount * 0.8 * 1));
            await _fileUtilityService.CreateFolder(tempPath);

            var tempInputFile = $"{await _fileUtilityService.NewTempFileName(tempPath)}.pdf";
            await _fileUtilityService.CreateFileAsync(pdfDocument, tempInputFile);

            var pageCount = 0;
            var digits = 0;
            var imageBytes = File.ReadAllBytes(tempInputFile);

            using (var image = NetVips.Image.NewFromBuffer(imageBytes))
            {
                pageCount = (int)image.Get("n-pages");
                digits = pageCount.ToString().Length;

                var pages = Enumerable.Range(0, pageCount);
                await pages.ParallelForEachAsync(async currentPage =>
                {
                    using (var page = NetVips.Image.PdfloadBuffer(imageBytes, page: currentPage, n: 1,
                        dpi: Convert.ToInt32(_configuration["Application:Tesseract:Dpi"].Split(" ")[1])))
                    {
                        var filename = Path.Combine(tempPath, $"{Path.GetFileNameWithoutExtension(tempInputFile)}-{currentPage.ToString($"D{digits}")}{extension}");
                        
                        _logger.LogInformation($"Rendering {filename} ...");
                        page.WriteToFile(filename);
                        _logger.LogInformation($"Rendered {filename} ...");

                        _logger.LogInformation($"Binaring image {filename} ...");
                        var bmp = new Bitmap(filename);
                        bmp = await _openCvService.DeskewAsync(await _openCvService.ImageSmootheningAsync(bmp));
                        bmp.Save(filename);
                        _logger.LogInformation($"Image binared {filename} ...");

                        bmp.Dispose();
                        bmp = null;
                        filename = null;
                    }
                }, _maxthreads);

                pages = null;
            }
            imageBytes = null;

            return tempPath;
        }

        public async Task<int> GetNumberOfPageAsync(byte[] pdfDocument)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").ToUpper());
            try
            {
                await _fileUtilityService.CreateFolder(tempPath);

                var tempInputFile = $"{await _fileUtilityService.NewTempFileName(tempPath)}.pdf";
                await _fileUtilityService.CreateFileAsync(pdfDocument, tempInputFile);

                var imageBytes = File.ReadAllBytes(tempInputFile);
                using (var image = NetVips.Image.NewFromBuffer(imageBytes))
                {
                    return (int)image.Get("n-pages");
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
