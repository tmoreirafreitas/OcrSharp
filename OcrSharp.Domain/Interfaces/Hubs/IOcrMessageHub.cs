using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Hubs
{
    public interface IOcrMessageHub
    {
        Task ImageMessage(string stream, StatusMessage status = StatusMessage.INFORMATIVE);
    }
}
