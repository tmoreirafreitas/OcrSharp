using Microsoft.Extensions.Logging;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace OcrSharp.Service
{
    public class FileUtilityService : IFileUtilityService
    {
        private readonly ILogger _logger;
        public FileUtilityService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<FileUtilityService>();
        }

        public async Task CreateFileAsync(byte[] data, string fullFileName, CancellationToken stoppingToken = default)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentNullException("Param data can not be null.");

            if (!File.Exists(fullFileName))
            {
                using (var fs = File.Create(fullFileName))
                {
                    await fs.WriteAsync(data, 0, data.Length, stoppingToken);
                }
            }
            else
            {
                _logger.LogInformation("File \"{0}\" already exists.", fullFileName);
            }
        }

        public async Task CreateFileAsync(Stream data, string fullFileName, CancellationToken stoppingToken = default)
        {
            if (data == null)
                throw new ArgumentNullException("Param data can not be null.");

            if (!File.Exists(fullFileName))
            {
                using (var fs = File.Create(fullFileName))
                {
                    await data.CopyToAsync(fs, stoppingToken);
                }
            }
            else
            {
                _logger.LogInformation("File \"{0}\" already exists.", fullFileName);
            }
        }

        public async Task<string> CreateFolder(string folderName, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                _logger.LogInformation($"Creating folder {folderName}");
                string pathString = string.Empty;
                if (!string.IsNullOrEmpty(folderName))
                {
                    pathString = folderName;
                    if (!Directory.Exists(pathString))
                        Directory.CreateDirectory(pathString);
                }
                return pathString;
            }, cancellationToken);
        }

        public async Task DeleteAllAsync(string directory, bool recursive = false, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive);
            }, cancellationToken);
        }

        public async Task DeleteFile(string fullFileName, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                if (File.Exists(fullFileName))
                    File.Delete(fullFileName);
            }, cancellationToken);
        }

        public async Task<IEnumerable<InMemoryFile>> GetFiles(string folderPath, string extension = "*.txt", CancellationToken cancellationToken = default)
        {
            var files = new List<InMemoryFile>();
            var filesNames = Directory.EnumerateFiles(folderPath, extension);
            foreach (var file in filesNames)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await DeleteAllAsync(folderPath, true);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                using (var fs = File.OpenRead(file))
                {
                    using (var ms = new MemoryStream())
                    {
                        await fs.CopyToAsync(ms, cancellationToken);
                        ms.Position = 0;
                        files.Add(new InMemoryFile { FileName = file, Content = ms.ToArray() });
                    }
                }                
            }

            Directory.Delete(folderPath, true);
            return files;
        }

        public async Task<Stream> GetZipArchive(IEnumerable<InMemoryFile> files, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var archiveStream = new MemoryStream();
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
                {
                    foreach (var file in files)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            cancellationToken.ThrowIfCancellationRequested();

                        var zipArchiveEntry = archive.CreateEntry(Path.GetFileName(file.FileName), CompressionLevel.Fastest);
                        using (var zipStream = zipArchiveEntry.Open())
                            zipStream.Write(file.Content, 0, file.Content.Length);
                    }
                }
                archiveStream.Position = 0;
                return archiveStream;
            }, cancellationToken);
        }
    }
}