using Microsoft.Extensions.Logging;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OcrSharp.Service
{
    /// <summary>
    /// Service used for manipulating files and directories.
    /// </summary>
    public class FileUtilityService : IFileUtilityService
    {
        private readonly ILogger _logger;
        /// <summary>
        ///  Initializes a new instance of the <see cref="FileUtilityService"/> class.
        /// </summary>
        /// <param name="loggerFactory">Service used for logging.</param>
        public FileUtilityService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<FileUtilityService>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="fullFileName"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="fullFileName"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="folderName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="recursive"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteAllAsync(string directory, bool recursive = false, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive);
            }, cancellationToken);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fullFileName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteFile(string fullFileName, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                if (File.Exists(fullFileName))
                    File.Delete(fullFileName);
            }, cancellationToken);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="extension"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="files"></param>
        /// <param name="extension"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<Stream> GetZipArchive(IEnumerable<Stream> files, string extension, CancellationToken cancellationToken = default)
        {
            if(files != null && files.Any())
            {
                var filenameWithoutExtensionAndPage = Guid.NewGuid().ToString("N").ToUpper();
                var count = files.Count();
                var digits = count.ToString().Length;
                var arrayFiles = files.ToArray();
                Stream archiveStream = new MemoryStream();

                try
                {
                    using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
                    {
                        for (var i = 0; i < count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var filename = $"{filenameWithoutExtensionAndPage}-{i.ToString($"D{digits}")}{extension}";
                            var zipArchiveEntry = archive.CreateEntry(filename, CompressionLevel.Fastest);

                            using var ms = new MemoryStream();
                            arrayFiles[i].CopyTo(ms);
                            var data = ms.ToArray();

                            using var zipStream = zipArchiveEntry.Open();
                            zipStream.Write(data, 0, data.Length);
                        }

                        arrayFiles = null;
                    }
                    archiveStream.Position = 0;
                    return Task.FromResult(archiveStream);
                }
                catch (Exception)
                {
                    throw;
                }
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tempPath"></param>
        /// <returns></returns>
        public Task<string> NewTempFileName(string tempPath)
        {
            return Task.FromResult(Path.Combine(tempPath, Guid.NewGuid().ToString("N").ToUpper()));
        }
    }
}