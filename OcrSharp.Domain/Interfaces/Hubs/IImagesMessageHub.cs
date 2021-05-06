using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Hubs
{
    public interface IImagesMessageHub
    {
        Task ImageMessage(string stream, StatusMensagem status = StatusMensagem.INFORMATIVO);
    }
}
