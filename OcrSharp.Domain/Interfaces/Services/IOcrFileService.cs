using OcrSharp.Domain.Entities;
using System.IO;
using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Services
{
    public interface IOcrFileService : IDomainService
    {
        //Task<InMemoryFile> ApplyOcrAsync(string fullFileName);
        Task<InMemoryFile> ApplyOcrAsync(Stream stream, bool bestOcuracy = false);
        Task<InMemoryFile> ApplyOcrAsync(InMemoryFile inMemory, bool bestOcuracy = false);
        InMemoryFile TextDetectionAndRecognitionToConvertTables(string fullFileName, int NoCols = 4, float MorphThrehold = 30f, 
            int binaryThreshold = 200, int offset = 5, double factor = 1.3, bool bestOcuracy = false);
    }
}
