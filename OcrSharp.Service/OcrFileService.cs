using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OcrSharp.Domain;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Services;
using OcrSharp.Service.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace OcrSharp.Service
{
    public class OcrFileService : IOcrFileService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IHubContext<StreamingHub> _streaming;
        private readonly int maxthreads = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 7.0));

        private class ThreadState
        {
            public InMemoryFile Image { get; set; }
            public TesseractEngine Engine { get; set; }
        }

        public OcrFileService(IConfiguration configuration, ILoggerFactory loggerFactory, IHubContext<StreamingHub> hubContext)
        {
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<OcrFileService>();
            _streaming = hubContext;
        }

        public async Task<InMemoryFile> ApplyOcrAsync(InMemoryFile inMemory, Accuracy accuracy = Accuracy.Medium)
        {
            var filename = $"{Path.GetFileNameWithoutExtension(inMemory.FileName)}.txt";
            _logger.LogInformation($"Pré-Processando a imagem {filename}");
            var file = await ApplyOcrAsync(inMemory.Content.ToStream(), accuracy);
            file.FileName = filename;
            file.Page = inMemory.Page;
            return file;
        }

        public async Task<InMemoryFile> ApplyOcrAsync(Stream stream, Accuracy accuracy = Accuracy.Medium)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            _logger.LogInformation("Removendo ruídos, suavizando e rotacionando a imagem.");
            var stResult = ImageForOcr(ref stream);
            _logger.LogInformation("Pré-Processamento finalizado.");
            _logger.LogInformation($"Aplicando OCR na acurácia {accuracy}.");
            var file = await ProcessOcrAsync(stResult, accuracy);
            _logger.LogInformation("OCR finalizado.");
            stopWatch.Stop();

            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);

            file.RunTime = elapsedTime;
            return file;
        }

        public async Task<IList<InMemoryFile>> ApplyOcrAsync(string connectionId, IList<InMemoryFile> images, Accuracy accuracy = Accuracy.Medium)
        {
            string tessDataPath = string.Empty;
            EngineMode mode = EngineMode.Default;
            switch (accuracy)
            {
                case Accuracy.Hight:
                    tessDataPath = _configuration["Tesseract:tessDataBest"];
                    break;
                case Accuracy.Low:
                    tessDataPath = _configuration["Tesseract:tessDataFast"];
                    break;
                case Accuracy.Medium:
                    tessDataPath = _configuration["Tesseract:tessData"];
                    break;
            }

            _logger.LogInformation("Removendo ruídos, suavizando e rotacionando as imagens.");

            var imagesProcessed = new List<InMemoryFile>();
            var tasks = new List<Task>();
            var index = 0;
            Stopwatch watch = new Stopwatch();

            object locker = new object();
            await images.AsyncParallelForEach(async image =>
            {
                watch.Start();
                ++index;
                var imageResult = await ProcessImage(image);
                lock (locker)
                {
                    imagesProcessed.Add(imageResult);

                    if (index % maxthreads == 0 || index == images.Count)
                    {
                        watch.Stop();

                        TimeSpan ts = watch.Elapsed;
                        string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                        ts.Hours, ts.Minutes, ts.Seconds,
                        ts.Milliseconds / 10);

                        _streaming.Clients.Client(connectionId).SendAsync("OcrStatusImagemProcess", $"Foram pré-processadas {imagesProcessed.Count} páginas em {elapsedTime}").Wait();
                    }
                }

            }, maxthreads);

            images.Clear();
            images = null;

            _logger.LogInformation("Pré-Processamento finalizado.");

            var ocrResult = new List<InMemoryFile>();
            Stopwatch stopWatch = new Stopwatch();

            var engines = new List<TesseractEngine>();
            for (var i = 0; i < maxthreads; i++)
                engines.Add(new TesseractEngine(tessDataPath, "por", mode));

            var engineIndex = -1;
            index = 0;

            foreach (var image in imagesProcessed)
            {
                stopWatch.Start();
                ++index;
                ++engineIndex;
                var engine = engines[engineIndex];

                tasks.Add(Task.Factory.StartNew((object obj) =>
                {
                    var data = obj as ThreadState;
                    if (data == null)
                        return;

                    using var pix = Pix.LoadFromMemory(data.Image.Content);
                    using var page = data.Engine.Process(pix, PageSegMode.AutoOsd);
                    engine.SetVariable("tessedit_write_images", true);
                    var text = Regex.Replace(page.GetText(), @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline).Trim('\f', '\n');
                    ocrResult.Add(new InMemoryFile
                    {
                        Content = Encoding.UTF8.GetBytes(text),
                        Accuracy = Math.Round(page.GetMeanConfidence(), 2),
                        AppliedOcr = true,
                        Page = image.Page
                    });
                }, new ThreadState { Image = image, Engine = engines[engineIndex] }));

                if (index % maxthreads == 0 || index == imagesProcessed.Count)
                {
                    engineIndex = -1;
                    Task.WaitAll(tasks.ToArray());
                    tasks.Clear();
                    stopWatch.Stop();
                    TimeSpan ts = stopWatch.Elapsed;
                    string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds,
                    ts.Milliseconds / 10);

                    var averageAccuracy = Math.Round(ocrResult.Where(x => x.Content.Length > 0 && x.Accuracy > 0).Average(x => x.Accuracy), 2);

                    _streaming.Clients.Client(connectionId).SendAsync("OcrStatusData", $"Processado {ocrResult.Count} de {imagesProcessed.Count} páginas em {elapsedTime} AverageAccuracy: {averageAccuracy}").Wait();
                }
            }

            index = 0;
            return ocrResult.OrderBy(x => x.Page).ToList();
        }

        private Task<InMemoryFile> ProcessImage(InMemoryFile image)
        {
            var stream = image.Content.ToStream();
            var imageResult = ImageForOcr(ref stream);
            return Task.FromResult(new InMemoryFile
            {
                Page = image.Page,
                FileName = image.FileName,
                Content = imageResult.ConvertToArray()
            });
        }

        private Task<InMemoryFile> ProcessOcrAsync(Stream stream, Accuracy accuracy = Accuracy.Medium)
        {
            string tessDataPath = string.Empty;
            switch (accuracy)
            {
                case Accuracy.Hight:
                    tessDataPath = _configuration["Tesseract:tessDataBest"];
                    break;
                case Accuracy.Low:
                    tessDataPath = _configuration["Tesseract:tessDataFast"];
                    break;
                case Accuracy.Medium:
                    tessDataPath = _configuration["Tesseract:tessData"];
                    break;
            }

            using var engine = new TesseractEngine(tessDataPath, "por+eng", EngineMode.Default);
            using var image = Pix.LoadFromMemory(stream.ConvertToArray());
            using var page = engine.Process(image, PageSegMode.AutoOsd);
            engine.SetVariable("tessedit_write_images", true);
            var ocrResult = Regex.Replace(page.GetText(), @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
            return Task.FromResult(new InMemoryFile
            {
                Content = Encoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.UTF8.GetBytes(ocrResult)),
                Accuracy = Math.Round(page.GetMeanConfidence(), 2),
                AppliedOcr = true
            });
        }

        private Stream ProcessDeskew(ref Bitmap tempImage)
        {
            Bitmap image;
            if (tempImage.PixelFormat.ToString().Equals("Format8bppIndexed"))
            {
                image = tempImage;
            }
            else
            {
                image = AForge.Imaging.Filters.Grayscale.CommonAlgorithms.BT709.Apply(tempImage);
            }

            AForge.Imaging.DocumentSkewChecker skewChecker = new AForge.Imaging.DocumentSkewChecker();
            // get documents skew angle
            double angle = skewChecker.GetSkewAngle(image);
            // create rotation filter
            AForge.Imaging.Filters.RotateBilinear rotationFilter = new AForge.Imaging.Filters.RotateBilinear(-angle);
            rotationFilter.FillColor = Color.Black;
            // rotate image applying the filter
            Bitmap rotatedImage = rotationFilter.Apply(image);

            var ms = new MemoryStream();
            rotatedImage.Save(ms, System.Drawing.Imaging.ImageFormat.Tiff);
            ms.Position = 0;

            image.Dispose();
            rotatedImage.Dispose();
            return ms;
        }

        private Stream Deskew(ref Stream stream)
        {
            Bitmap bmp = new Bitmap(stream);
            return ProcessDeskew(ref bmp);
        }

        private Stream Deskew(ref Bitmap image)
        {
            return ProcessDeskew(ref image);
        }

        public Stream ImageForOcr(ref Stream stream)
        {
            var st = ProcessImageForOcr(ref stream);
            return Deskew(ref st);
        }

        private Stream ProcessImageForOcr(ref Stream stream)
        {
            using Bitmap bmp = new Bitmap(stream);
            using var image = bmp.ToImage<Gray, byte>();
            var imageSmoothening = ImageSmoothening(image);

            var ms = new MemoryStream();
            imageSmoothening.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Tiff);
            ms.Position = 0;
            return ms;
        }

        private Image<Gray, byte> ImageSmoothening(Image<Gray, byte> image)
        {
            using var src = image.Copy();
            const int binaryThreshold = 127;
            var processedImage = image.CopyBlank();

            CvInvoke.Threshold(src, processedImage, binaryThreshold, 255, ThresholdType.Binary);
            CvInvoke.Threshold(processedImage, processedImage, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);
            CvInvoke.GaussianBlur(processedImage, processedImage, new Size(1, 1), 0);

            //processedImage = processedImage.Erode(1).Dilate(1);
            CvInvoke.Threshold(processedImage, processedImage, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);
            return processedImage;
        }

        private Image<Gray, byte> RemoveNoiseAndSmooth(ref Bitmap bmp)
        {
            const int imageSize = 1800;
            using Image<Gray, byte> image = bmp.ToImage<Gray, byte>();
            var factor = Math.Max(1, imageSize / image.Width);
            image.Resize(image.Width * factor, image.Height * factor, Inter.Cubic, true);

            using var filtered = image.CopyBlank();
            using var opening = image.CopyBlank();
            using var closing = image.CopyBlank();
            CvInvoke.AdaptiveThreshold(image, filtered, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, 85, 11);
            //CvInvoke.AdaptiveThreshold(image, filtered, 255, AdaptiveThresholdType.MeanC, ThresholdType.Binary, 41, 3);

            using var kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(1, 1), new Point(-1, -1));
            CvInvoke.MorphologyEx(filtered, opening, MorphOp.Open, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(1.0));
            CvInvoke.MorphologyEx(opening, closing, MorphOp.Close, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(1.0));

            using var imageSmoothening = ImageSmoothening(closing);
            var orImage = image.CopyBlank();
            CvInvoke.BitwiseOr(imageSmoothening, closing, orImage);
            return orImage;
        }
    }
}
