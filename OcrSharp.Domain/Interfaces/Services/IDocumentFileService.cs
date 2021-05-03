using OcrSharp.Domain.Entities;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Services
{
    public interface IDocumentFileService : IDomainService
    {
        Task<InMemoryFile> ConvertMultiplePdfToImageAsync(IEnumerable<InMemoryFile> fileCollection, CancellationToken cancellationToken = default(CancellationToken));
        InMemoryFile ConvertPdfPageToImageAsync(InMemoryFile file, int pageNumber);
        Task<DocumentPage> ExtracTextFromPdfPageAsync(InMemoryFile file, int pageNumber, Accuracy accuracy = Accuracy.Medium, CancellationToken cancellationToken = default(CancellationToken));
        Task<DocumentFile> ExtractTextFromPdf(string connectionId, InMemoryFile file, Accuracy accuracy = Accuracy.Medium, CancellationToken cancellationToken = default(CancellationToken));
        Task<Stream> ConvertPdfFileToImagesZippedAsync(InMemoryFile file);
        int GetNumberOfPages(InMemoryFile file);
    }
}
