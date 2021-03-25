using OcrSharp.Domain.Entities;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Services
{
    public interface IDocumentFileService : IDomainService
    {
        InMemoryFile ConvertMultiplePdfToImage(ref IEnumerable<InMemoryFile> fileCollection, CancellationToken cancellationToken = default(CancellationToken));
        InMemoryFile ConvertPdfPageToImage(InMemoryFile file, int pageNumber);
        Task<DocumentPage> ExtracTextFromPdfPageAsync(InMemoryFile file, int pageNumber, Accuracy accuracy = Accuracy.Medium, CancellationToken cancellationToken = default(CancellationToken));
        Stream ConvertPdfFileToImages(InMemoryFile file);
        Task<DocumentFile> ExtractTextFromPdf(InMemoryFile file, Accuracy accuracy = Accuracy.Medium, CancellationToken cancellationToken = default(CancellationToken));
        int GetNumberOfPages(InMemoryFile file);
    }
}
