using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Hubs
{
    public interface IOcrMessageHub
    {
        /// <summary>
        /// API de OCR de arquivos de JPG, PNG, GIF, TIFF e PDF
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        Task OcrMessage(string stream, StatusMessage status = StatusMessage.INFORMATIVE);
    }
}
