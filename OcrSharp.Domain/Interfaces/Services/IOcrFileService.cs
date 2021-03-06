﻿using OcrSharp.Domain.Entities;
using System.IO;
using System.Threading.Tasks;

namespace OcrSharp.Domain.Interfaces.Services
{
    public interface IOcrFileService : IDomainService
    {
        //Task<InMemoryFile> ApplyOcrAsync(string fullFileName);
        Task<InMemoryFile> ApplyOcrAsync(Stream stream, Accuracy accuracy = Accuracy.Medium);
        Task<InMemoryFile> ApplyOcrAsync(InMemoryFile inMemory, Accuracy accuracy = Accuracy.Medium);
        InMemoryFile TextDetectionAndRecognitionToConvertTables(string fullFileName, int NoCols = 4, float MorphThrehold = 30f, 
            int binaryThreshold = 200, int offset = 5, double factor = 1.3, Accuracy accuracy = Accuracy.Medium);
    }
}
