using OcrSharp.Domain.Entities;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Services
{
    public interface IPdfFileService : IDomainService
    {
        Task<InMemoryFile> ConvertMultiplePdfToImageAsync(IEnumerable<InMemoryFile> fileCollection, CancellationToken cancellationToken = default(CancellationToken));
        Task<InMemoryFile> ConvertPdfPageToImageAsync(InMemoryFile file, int pageNumber, CancellationToken cancellationToken = default(CancellationToken));
        Task<PdfPage> ExtracTextFromPdfPageAsync(InMemoryFile file, int pageNumber, CancellationToken cancellationToken = default(CancellationToken));
        Task<Stream> ConvertPdfFileToImagesAsync(InMemoryFile file);
        Task<PdfFile> ExtractTextFromPdf(InMemoryFile file, CancellationToken cancellationToken = default(CancellationToken));
    }
}
