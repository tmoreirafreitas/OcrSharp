﻿using OcrSharp.Domain.Entities;
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
    public class DocumentFileService : IDocumentFileService
    {
        private readonly IFileUtilityService _fileUtilityService;
        private readonly IOcrFileService _ocrFileService;

        public DocumentFileService(IFileUtilityService fileUtilityService, IOcrFileService ocrFileService)
        {
            _fileUtilityService = fileUtilityService;
            _ocrFileService = ocrFileService;
        }

        public InMemoryFile ConvertMultiplePdfToImage(ref IEnumerable<InMemoryFile> fileCollection, CancellationToken cancellationToken = default(CancellationToken))
        {
            var filesToZip = new List<InMemoryFile>();

            foreach (var file in fileCollection)
            {
                if (cancellationToken.IsCancellationRequested)
                    cancellationToken.ThrowIfCancellationRequested();

                var fileZipStream = ConvertPdfFileToImages(file);

                filesToZip.Add(new InMemoryFile
                {
                    FileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_Images_Results_{DateTime.Now.GetDateNowEngFormat()}.zip",
                    Content = fileZipStream.StreamToArrayAsync(cancellationToken).Result
                });
            }

            var zipFile = _fileUtilityService.GetZipArchive(filesToZip).Result;
            var filename = $"IMAGES_RESULTS_{DateTime.Now.GetDateNowEngFormat()}.zip";
            return new InMemoryFile
            {
                FileName = filename,
                Content = zipFile.StreamToArrayAsync(cancellationToken).Result
            };
        }

        public async Task<DocumentPage> ExtracTextFromPdfPageAsync(InMemoryFile file, int pageNumber, bool bestOcuracy = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var document = UglyToad.PdfPig.PdfDocument.Open(file.Content))
            {
                InMemoryFile inMemoryFile = null;
                var sb = new StringBuilder();
                var pdfPage = document.GetPage(pageNumber);
                var works = pdfPage.GetWords();
                bool applyedOcr = false;
                if (works.Any())
                    sb.Append(string.Join(" ", works.Select(x => x.Text)).Replace("\r", "\r\n"));
                else
                {
                    var image = ConvertPdfPageToImage(file, pageNumber);
                    if (image != null)
                    {
                        inMemoryFile = await _ocrFileService.ApplyOcrAsync(new MemoryStream(image.Content), bestOcuracy);
                        sb.Append(Encoding.UTF8.GetString(inMemoryFile.Content));
                        applyedOcr = true;
                    }
                }
                var page = new DocumentPage(pageNumber, sb.ToString(), applyedOcr);
                if(page.AppliedOcr)
                {
                    page.Accuracy = inMemoryFile.Accuracy;
                }
                return page;
            }
        }

        public async Task<DocumentFile> ExtractTextFromPdf(InMemoryFile file, bool bestOcuracy = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var doc = UglyToad.PdfPig.PdfDocument.Open(file.Content))
            {
                var pagesCount = doc.GetPages().Count();
                var pdf = new DocumentFile(pagesCount, file.FileName);
                var index = 0;
                var pages = doc.GetPages().ToAsyncEnumerable();

                object listLock = new object();
                await pages.AsyncParallelForEach(async entry =>
                {
                    ++index;
                    var pdfPage = await ExtracTextFromPdfPageAsync(file, index, bestOcuracy);
                    lock (listLock)
                        pdf.Pages.Add(pdfPage);
                },
                    Convert.ToInt32(Math.Ceiling(Environment.ProcessorCount * 0.25 * 2.0)));

                return pdf;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdfFilename"></param>
        /// <returns>return the zipped image files</returns>
        public Stream ConvertPdfFileToImages(InMemoryFile file)
        {
            ICollection<InMemoryFile> files = new List<InMemoryFile>();
            var outputImagePath = string.Empty;

            var pagesCount = 0;
            using (var document = PdfDocument.Load(file.Content.ArrayToStream()))
                pagesCount = document.PageCount;

            InMemoryFile fileInMemory = null;
            object listLock = new object();
            Parallel.For(1, pagesCount, new ParallelOptions
            {
                // multiply the count because a processor can have 2 cores
                MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling(Environment.ProcessorCount * 0.25 * 2.0))
            }, i =>
            {
                fileInMemory = ConvertPdfPageToImage(file, i);
                if (fileInMemory != null)
                    lock (listLock)
                        files.Add(fileInMemory);
            });

            return _fileUtilityService.GetZipArchive(files).Result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="pagina"></param>
        /// <returns>returns the page of the pdf file converted to png image</returns>
        public InMemoryFile ConvertPdfPageToImage(InMemoryFile file, int pageNumber)
        {
            var xDpi = 300; //set the x DPI
            var yDpi = 300; //set the y DPI

            InMemoryFile fileInMemory = null;

            var pdfFilename = $"{ Path.GetFileNameWithoutExtension(file.FileName)}_Pagina_{pageNumber:D3}.tiff";

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
                using (var document = PdfDocument.Load(file.Content.ArrayToStream()))
                using (var image = document.Render(pageNumber - 1, xDpi, yDpi, PdfRenderFlags.CorrectFromDpi))
                using (var stream = new MemoryStream())
                {
                    image.Save(stream, System.Drawing.Imaging.ImageFormat.Tiff);
                    stream.Position = 0;
                    fileInMemory = new InMemoryFile
                    {
                        FileName = pdfFilename,
                        Content = stream.ToArray()
                    };
                }
            }

            return fileInMemory;
        }
    }
}