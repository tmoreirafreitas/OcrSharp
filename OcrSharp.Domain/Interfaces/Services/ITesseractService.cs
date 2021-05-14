using OcrSharp.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Services
{
    /// <summary>
    /// Service to read texts from images through OCR Tesseract engine.
    /// </summary>
    public interface ITesseractService : IDomainService
    {
        Task<IList<InMemoryFile>> GetText(IList<InMemoryFile> images, Accuracy accuracy);
        /// <summary>
        /// Method used to return texts extracted from images.
        /// </summary>
        /// <param name="tempPath">The directory of the location of the image files for the OCR Tesseract</param>
        /// <param name="extension">Extension of the files used for the CR Tesseract engine</param>
        /// <param name="accuracy">Accuracy is used to define the training file by the CR Tesseract engine for extracting text from the image</param>
        /// <returns>Returns a list of pages with the text extracted from the image</returns>
        Task<IList<DocumentPage>> GetDocumentPages(string tempPath, string extension, Accuracy accuracy);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tempInputFile"></param>
        /// <param name="extension"></param>
        /// <param name="accuracy"></param>
        /// <returns></returns>
        Task<string> GetText(string tempInputFile, string extension, Accuracy accuracy);
    }
}
