using OcrSharp.Domain.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Services
{
    public interface IOcrFileService : IDomainService
    {
        Task<InMemoryFile> ApplyOcrAsync(Stream stream, Accuracy accuracy = Accuracy.Low);
        Task<InMemoryFile> ApplyOcrAsync(InMemoryFile inMemory, Accuracy accuracy = Accuracy.Low);
        Task<IList<InMemoryFile>> ApplyOcrAsync(string connectionId, IList<InMemoryFile> images, Accuracy accuracy = Accuracy.Low);
        Stream ImageForOcr(Stream stream);
    }
}
