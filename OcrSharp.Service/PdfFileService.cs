using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service.Extensions;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OcrSharp.Service
{
    public class PdfFileService : IPdfFileService
    {
        private readonly IFileUtilityService _fileUtilityService;
        private readonly IOcrFileService _ocrFileService;

        public PdfFileService(IFileUtilityService fileUtilityService, IOcrFileService ocrFileService)
        {
            _fileUtilityService = fileUtilityService;
            _ocrFileService = ocrFileService;
        }

        public async Task<InMemoryFile> ConvertMultiplePdfToImageAsync(IEnumerable<InMemoryFile> fileCollection, CancellationToken cancellationToken = default(CancellationToken))
        {
            var filesToZip = new List<InMemoryFile>();

            foreach (var file in fileCollection)
            {
                var fileZipStream = await ConvertPdfFileToImagesAsync(file);

                filesToZip.Add(new InMemoryFile
                {
                    FileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_Images_Results_{DateTime.Now.GetDateNowEngFormat()}.zip",
                    Content = await fileZipStream.StreamToArrayAsync(cancellationToken)
                });
            }

            var zipFile = await _fileUtilityService.GetZipArchive(filesToZip);
            var filename = $"IMAGES_RESULTS_{DateTime.Now.GetDateNowEngFormat()}.zip";
            return new InMemoryFile
            {
                FileName = filename,
                Content = await zipFile.StreamToArrayAsync(cancellationToken)
            };
        }

        public async Task<PdfPage> ExtracTextFromPdfPageAsync(InMemoryFile file, int pageNumber, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await Task.Run(async () =>
            {
                using var document = UglyToad.PdfPig.PdfDocument.Open(file.Content);
                InMemoryFile inMemoryFile;
                var sb = new StringBuilder();
                var pdfPage = document.GetPage(pageNumber);
                var works = pdfPage.GetWords();
                if (works.Any())
                    sb.Append(string.Join(" ", works.Select(x => x.Text)).Replace("\r", "\r\n"));
                else
                {
                    var image = pdfPage.GetImages().FirstOrDefault();
                    if (image != null)
                    {
                        inMemoryFile = await _ocrFileService.ApplyOcrAsync(new MemoryStream(image.RawBytes.ToArray()));
                        sb.Append(Encoding.UTF8.GetString(inMemoryFile.Content));
                    }
                }
                return new PdfPage(pageNumber, sb.ToString());
            }, cancellationToken);
        }

        public async Task<PdfFile> ExtractTextFromPdf(InMemoryFile file, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await Task.Run(async () =>
            {
                var outputImagePath = string.Empty;

                var pagesCount = 0;
                using (var document = PdfDocument.Load(file.Content.ArrayToStream()))
                    pagesCount = document.PageCount;

                var pdf = new PdfFile(pagesCount, file.FileName);
                for (var i = 1; i <= pagesCount; i++)
                {
                    var page = await ExtracTextFromPdfPageAsync(file, i, cancellationToken);
                    if (page != null)
                        pdf.Pages.Add(page);
                }

                return pdf;
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdfFilename"></param>
        /// <returns>return the zipped image files</returns>
        public async Task<Stream> ConvertPdfFileToImagesAsync(InMemoryFile file)
        {
            return await Task.Run(async () =>
            {
                ICollection<InMemoryFile> files = new List<InMemoryFile>();
                var outputImagePath = string.Empty;

                var pagesCount = 0;
                using (var document = PdfDocument.Load(file.Content.ArrayToStream()))
                    pagesCount = document.PageCount;

                InMemoryFile fileInMemory = null;

                for (var i = 1; i <= pagesCount; i++)
                {
                    fileInMemory = await ConvertPdfPageToImageAsync(file, i);
                    if (fileInMemory != null)
                        files.Add(fileInMemory);
                }

                return await _fileUtilityService.GetZipArchive(files);
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="pagina"></param>
        /// <returns>returns the page of the pdf file converted to png image</returns>
        public async Task<InMemoryFile> ConvertPdfPageToImageAsync(InMemoryFile file, int pageNumber, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await Task.Run(() =>
            {
                var xDpi = 300; //set the x DPI
                var yDpi = 300; //set the y DPI

                InMemoryFile fileInMemory = null;

                var pdfFilename = $"{ Path.GetFileNameWithoutExtension(file.FileName)}_Pagina_{pageNumber:D3}.png";

                using (var pigDocument = UglyToad.PdfPig.PdfDocument.Open(file.Content))
                {
                    if (!string.IsNullOrEmpty(pigDocument.GetPage(pageNumber).Text) || !string.IsNullOrWhiteSpace(pigDocument.GetPage(pageNumber).Text))
                    {
                        var image = pigDocument.GetPage(pageNumber).GetImages().FirstOrDefault();
                        if (image != null)
                        {
                            fileInMemory = new InMemoryFile
                            {
                                FileName = pdfFilename,
                                Content = image.RawBytes.ToArray()
                            };
                        }
                    }
                }


                if (fileInMemory == null)
                {
                    using var document = PdfDocument.Load(file.Content.ArrayToStream());
                    using var image = document.Render(pageNumber - 1, xDpi, yDpi, true);
                    using var stream = new MemoryStream();
                    image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;
                    fileInMemory = new InMemoryFile
                    {
                        FileName = pdfFilename,
                        Content = stream.ToArray()
                    };
                }

                return fileInMemory;
            }, cancellationToken);
        }
    }
}
