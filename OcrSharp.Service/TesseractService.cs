using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OcrSharp.Domain;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Hubs;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Domain.Options;
using OcrSharp.Service.Extensions;
using OcrSharp.Service.Hubs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OcrSharp.Service
{
    /// <summary>
    /// Service to read texts from images through OCR Tesseract engine.
    /// </summary>
    public class TesseractService : ITesseractService
    {
        private readonly ILogger<TesseractService> _logger;
        private readonly IFileUtilityService _fileUtilityService;
        private readonly IHubContext<ImagesMessageHub, IImagesMessageHub> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly TesseractOptions _tesseractOptions;
        private readonly int _maxthreads = Convert.ToInt32(Math.Ceiling(Environment.ProcessorCount * 0.8 * 1));

        /// <summary>
        /// Initializes a new instance of the <see cref="TesseractService"/> class.
        /// </summary>
        /// <param name="configuration">The configuration </param>
        /// <param name="fileUtilityService"></param>
        /// <param name="hubContext"></param>
        /// <param name="logger"></param>
        /// <param name="tesseractOptions">The language used to extract text from images (eng, por, etc)
        /// The path for the Tesseract4 installation folder (C:\Program Files\Tesseract-OCR).
        /// </param>
        public TesseractService(ILogger<TesseractService> logger,
            IFileUtilityService fileUtilityService,
            IHubContext<ImagesMessageHub, IImagesMessageHub> hubContext,
            IConfiguration configuration,
            IOptions<TesseractOptions> tesseractOptions)
        {
            _fileUtilityService = fileUtilityService;
            _tesseractOptions = tesseractOptions.Value;
            _configuration = configuration;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<IList<InMemoryFile>> GetText(IList<InMemoryFile> images, Accuracy accuracy)
        {
            var resultOcr = new List<InMemoryFile>();
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                if (images.Any())
                {
                    string tessDataPath = string.Empty;
                    switch (accuracy)
                    {
                        case Accuracy.Hight:
                            tessDataPath = _configuration["Application:Tesseract:tessDataBest"];
                            break;
                        case Accuracy.Low:
                            tessDataPath = _configuration["Application:Tesseract:tessDataFast"];
                            break;
                        case Accuracy.Medium:
                            tessDataPath = _configuration["Application:Tesseract:tessData"];
                            break;
                    }
                    Environment.SetEnvironmentVariable("TESSDATA_PREFIX", tessDataPath);
                    Environment.SetEnvironmentVariable("OMP_THREAD_LIMIT", _tesseractOptions.ThreadLimit);

                    await _fileUtilityService.CreateFolder(tempPath);
                    var totalPages = images.Count();
                    var numberPage = 0;
                    var locker = new object();
                    await images.ParallelForEachAsync(async image =>
                    {
                        ++numberPage;
                        var tempInputFile = $"{_fileUtilityService.NewTempFileName(tempPath)}-{image.FileName}-{image.Page}.png";
                        var tempOutputFile = $"{_fileUtilityService.NewTempFileName(tempPath)}-{image.FileName}-{image.Page}.txt";
                        await _fileUtilityService.CreateFileAsync(image.Content.ToStream(), tempInputFile);

                        var args = $"{tempInputFile} {tempOutputFile} -l {_tesseractOptions.Language} {_tesseractOptions.Oem} {_tesseractOptions.Psm} {_tesseractOptions.Dpi}";
                        var info = new ProcessStartInfo(_tesseractOptions.TesseractExe, args)
                        {
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };

                        using (var ps = Process.Start(info))
                        {
                            _logger.LogInformation($"Running Process Asynchronously: Filename = {tempInputFile}, Arguments = {args}");
                            await ps.WaitForExitAsync();

                            var exitCode = ps.ExitCode;

                            if (exitCode == 0)
                            {
                                var tmpOutput = File.ReadAllText(tempOutputFile + ".txt");
                                var textResult = Regex.Replace(tmpOutput, @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
                                _logger.LogInformation($"ResultText = {textResult}");
                                lock (locker)
                                    resultOcr.Add(new InMemoryFile
                                    {
                                        AppliedOcr = true,
                                        FileName = image.FileName,
                                        Page = image.Page,
                                        Content = Encoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.UTF8.GetBytes(textResult))
                                    });
                            }
                            else
                            {
                                var stderr = ps.StandardError.ReadToEnd();
                                throw new InvalidOperationException(stderr);
                            }
                        }
                    }, _maxthreads);
                }
            }
            finally
            {
                Directory.Delete(tempPath, true);
                images.Clear();
                images = null;
            }

            return resultOcr.OrderBy(x => x.Page).ToList();
        }

        /// <summary>
        /// Method used to return texts extracted from images.
        /// </summary>
        /// <param name="tempPath">The directory of the location of the image files for the OCR Tesseract</param>
        /// <param name="extension">Extension of the files used for the CR Tesseract engine</param>
        /// <param name="accuracy">Accuracy is used to define the training file by the CR Tesseract engine for extracting text from the image</param>
        /// <returns>Returns a list of pages with the text extracted from the image</returns>
        public async Task<IList<DocumentPage>> GetDocumentPages(string tempPath, string extension, Accuracy accuracy)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            string tessDataPath = string.Empty;
            switch (accuracy)
            {
                case Accuracy.Hight:
                    tessDataPath = _configuration["Application:Tesseract:tessDataBest"];
                    break;
                case Accuracy.Low:
                    tessDataPath = _configuration["Application:Tesseract:tessDataFast"];
                    break;
                case Accuracy.Medium:
                    tessDataPath = _configuration["Application:Tesseract:tessData"];
                    break;
            }
            Environment.SetEnvironmentVariable("TESSDATA_PREFIX", tessDataPath);
            Environment.SetEnvironmentVariable("OMP_THREAD_LIMIT", _tesseractOptions.ThreadLimit);

            try
            {
                var files = Directory.GetFiles(tempPath, extension);
                var totalPages = files.Count();
                var locker = new object();
                var docPages = new List<DocumentPage>();
                await files.ParallelForEachAsync(async file =>
                {
                    var page = Convert.ToInt32(Path.GetFileNameWithoutExtension(file).Split("-")[1]);
                    var tempOutputFile = $"{Path.ChangeExtension(file, ".txt")}";
                    var args = $"{file} {tempOutputFile} -l {_tesseractOptions.Language} {_tesseractOptions.Oem} {_tesseractOptions.Psm} {_tesseractOptions.Dpi}";
                    var info = new ProcessStartInfo(_tesseractOptions.TesseractExe, args)
                    {
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    using (var ps = Process.Start(info))
                    {
                        _logger.LogInformation($"Running Process Asynchronously: Filename = {file}, Arguments = {args}");
                        await ps.WaitForExitAsync();

                        var exitCode = ps.ExitCode;

                        if (exitCode == 0)
                        {
                            var tmpOutput = File.ReadAllText(tempOutputFile + ".txt");
                            var textResult = Regex.Replace(tmpOutput, @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
                            stopWatch.Stop();

                            TimeSpan el = stopWatch.Elapsed;
                            string strElapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                            el.Hours, el.Minutes, el.Seconds,
                            el.Milliseconds / 10);
                            lock (locker)
                            {
                                docPages.Add(new DocumentPage(page, textResult)
                                {
                                    AppliedOcr = true,
                                    RunTime = strElapsedTime,
                                });

                                _logger.LogInformation($"Processado {docPages.Count} de {totalPages}");
                            }

                            tmpOutput = null;
                            textResult = null;
                            strElapsedTime = null;
                        }
                        else
                        {
                            var stderr = ps.StandardError.ReadToEnd();
                            _logger.LogInformation(stderr);
                            throw new InvalidOperationException(stderr);
                        }
                        tempOutputFile = null;
                        args = null;
                        info = null;
                    }
                }, _maxthreads);
                files = null;
                locker = null;
                return docPages;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<string> GetText(string tempInputFile, string extension, Accuracy accuracy)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            string tessDataPath = string.Empty;
            switch (accuracy)
            {
                case Accuracy.Hight:
                    tessDataPath = _configuration["Application:Tesseract:tessDataBest"];
                    break;
                case Accuracy.Low:
                    tessDataPath = _configuration["Application:Tesseract:tessDataFast"];
                    break;
                case Accuracy.Medium:
                    tessDataPath = _configuration["Application:Tesseract:tessData"];
                    break;
            }
            Environment.SetEnvironmentVariable("TESSDATA_PREFIX", tessDataPath);
            Environment.SetEnvironmentVariable("OMP_THREAD_LIMIT", _tesseractOptions.ThreadLimit);

            try
            {
                var tempOutputFile = $"{Path.ChangeExtension(tempInputFile, ".txt")}";
                var args = $"{tempInputFile} {tempOutputFile} -l {_tesseractOptions.Language} {_tesseractOptions.Oem} {_tesseractOptions.Psm} {_tesseractOptions.Dpi}";
                var info = new ProcessStartInfo(_tesseractOptions.TesseractExe, args)
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (var ps = Process.Start(info))
                {
                    _logger.LogInformation($"Running Process Asynchronously: Filename = {tempInputFile}, Arguments = {args}");
                    await ps.WaitForExitAsync();

                    var exitCode = ps.ExitCode;

                    if (exitCode == 0)
                    {
                        var tmpOutput = File.ReadAllText(tempOutputFile + ".txt");
                        var textResult = Regex.Replace(tmpOutput, @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
                        stopWatch.Stop();

                        TimeSpan el = stopWatch.Elapsed;
                        string strElapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                        el.Hours, el.Minutes, el.Seconds,
                        el.Milliseconds / 10);
                        _logger.LogInformation($"Runned Process Asynchronously: Time = {strElapsedTime}");
                        tmpOutput = null;
                        strElapsedTime = null;
                        return textResult;
                    }
                    else
                    {
                        var stderr = ps.StandardError.ReadToEnd();
                        _logger.LogInformation(stderr);
                        throw new InvalidOperationException(stderr);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}