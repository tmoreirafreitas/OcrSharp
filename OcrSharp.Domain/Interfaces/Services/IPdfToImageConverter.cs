using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Services
{
    /// <summary>
    /// Service to convert pdf file to image using the NetVips engine
    /// </summary>
    public interface IPdfToImageConverter : IDomainService
    {
        Task<IEnumerable<Stream>> ConvertToStreams(byte[] pdfDocument, string extension);
        Task<IEnumerable<byte[]>> ConvertToBuffers(byte[] pdfDocument, string extension);
        Task<Stream> ConvertPdfPageToImageStream(byte[] pdfDocument, int page, string extension);
        Task<string> ConvertToFiles(byte[] pdfDocument, string extension);
        Task<string> ConvertToFilesPreProcessed(byte[] pdfDocument, string extension);
        Task<int> GetNumberOfPageAsync(byte[] pdfDocument);
    }
}
