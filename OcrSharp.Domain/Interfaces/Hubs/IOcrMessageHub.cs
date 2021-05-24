using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Hubs
{
    public interface IOcrMessageHub
    {
        Task OcrMessage(string stream, StatusMessage status = StatusMessage.INFORMATIVE);
    }
}
