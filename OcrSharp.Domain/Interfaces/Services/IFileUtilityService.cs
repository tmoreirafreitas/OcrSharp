using OcrSharp.Domain.Entities;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Services
{
    public interface IFileUtilityService : IDomainService
    {
        Task<string> CreateFolder(string folderName, CancellationToken cancellationToken = default);
        Task CreateFileAsync(byte[] data, string fullFileName, CancellationToken stoppingToken = default);
        Task CreateFileAsync(Stream data, string fullFileName, CancellationToken stoppingToken = default);
        Task DeleteAllAsync(string directory, bool recursive = false, CancellationToken cancellationToken = default);
        Task DeleteFile(string fullFileName, CancellationToken cancellationToken = default);
        //Task<Stream> GetZipArchive(IEnumerable<InMemoryFile> files, CancellationToken cancellationToken = default);
        Task<Stream> GetZipArchive(IEnumerable<Stream> files, string extension, CancellationToken cancellationToken = default);
        Task<IEnumerable<InMemoryFile>> GetFiles(string folderPath, string extension = "*.txt", CancellationToken cancellationToken = default);
        Task<string> NewTempFileName(string tempPath);
    }
}
