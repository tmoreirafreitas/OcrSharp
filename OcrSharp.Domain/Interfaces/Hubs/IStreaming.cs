using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Hubs
{
    public interface IStreaming
    {
        Task ImageMessage(string stream, StatusMensagem status = StatusMensagem.INFORMATIVO);
    }
}
