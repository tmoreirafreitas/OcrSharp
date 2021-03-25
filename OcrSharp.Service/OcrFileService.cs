using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
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

        public OcrFileService(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<OcrFileService>();
        }

        public async Task<InMemoryFile> ApplyOcrAsync(InMemoryFile inMemory, Accuracy accuracy = Accuracy.Medium)
        {
            var filename = $"{Path.GetFileNameWithoutExtension(inMemory.FileName)}.txt";
            _logger.LogInformation($"Pré-Processando a imagem {filename}");
            var file = await ApplyOcrAsync(inMemory.Content.ArrayToStream(), accuracy);
            file.FileName = filename;
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

        private async Task<InMemoryFile> ProcessOcrAsync(Stream stream, Accuracy accuracy = Accuracy.Medium)
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
            using var image = Pix.LoadFromMemory(await stream.StreamToArrayAsync());
            using var page = engine.Process(image, PageSegMode.AutoOsd);
            engine.SetVariable("tessedit_write_images", true);
            var ocrResult = Regex.Replace(page.GetText(), @"^\s+$[\r\n]*", string.Empty);
            return new InMemoryFile
            {
                Content = Encoding.UTF8.GetBytes(ocrResult),
                Accuracy = page.GetMeanConfidence(),
                AppliedOcr = true
            };
        }

        private Stream ProcessDeskew(ref Bitmap bitmap, double maxSkew = 20.0)
        {
            Image<Gray, byte> rotatedImage = null;
            var stream = new MemoryStream();
            using (bitmap)
            {
                using var image = bitmap.ToImage<Gray, byte>();
                rotatedImage = image.CopyBlank();
                CvInvoke.FastNlMeansDenoising(image, rotatedImage);
                CvInvoke.Threshold(image, rotatedImage, 127, 255, ThresholdType.BinaryInv | ThresholdType.Otsu);
                rotatedImage = rotatedImage.Dilate(5);

                var allAngles = new List<double>();
                var wearableAngles = new List<double>();
                var lines = CvInvoke.HoughLinesP(rotatedImage, 1, Math.PI / 180, 100, minLineLength: image.Width / 12, maxGap: image.Width / 150);

                foreach (var line in lines)
                {
                    var x1 = line.P1.X;
                    var y1 = line.P1.Y;
                    var x2 = line.P2.X;
                    var y2 = line.P2.Y;
                    allAngles.Add(Math.Atan2(y2 - y1, x2 - x1));
                }

                //Se a maioria de nossas linhas são verticais, esta provavelmente é uma imagem de paisagem.
                var landscape = allAngles.Where(angle => Math.Abs(angle) > (Math.PI / 4)).Sum() > allAngles.Count / 2;

                if (landscape)
                {
                    wearableAngles = allAngles.Where(angle => ConvertToRadians(90 - maxSkew) < Math.Abs(angle) && Math.Abs(angle) < ConvertToRadians(90 + maxSkew)).ToList();
                }
                else
                {
                    wearableAngles = allAngles.Where(angle => Math.Abs(angle) < ConvertToRadians(maxSkew)).ToList();
                }

                var angle = 0.0;
                if (wearableAngles.Count > 5)
                {
                    angle = ConvertRadiansToDegrees(wearableAngles.Average());
                }

                if (landscape)
                {
                    if (angle < 0)
                    {
                        CvInvoke.Rotate(image, rotatedImage, RotateFlags.Rotate90Clockwise);
                        angle += 90;
                    }
                    else if (angle > 0)
                    {
                        CvInvoke.Rotate(image, rotatedImage, RotateFlags.Rotate90CounterClockwise);
                        angle -= 90;
                    }
                }

                using var rotationMatrix = new Mat(new Size(2, 3), DepthType.Cv32F, 1);
                CvInvoke.GetRotationMatrix2D(new PointF(image.Width / 2, image.Height / 2), angle, 1.0, rotationMatrix);
                CvInvoke.WarpAffine(image, rotatedImage, rotationMatrix, image.Size, Inter.Cubic, borderMode: BorderType.Replicate);

                allAngles.Clear();
                wearableAngles.Clear();
                lines = null;
                allAngles = null;
                wearableAngles = null;
            }

            rotatedImage.Convert<Bgr, byte>().ToBitmap().Save(stream, System.Drawing.Imaging.ImageFormat.Tiff);
            stream.Position = 0;
            rotatedImage.Dispose();
            rotatedImage = null;
            return stream;
        }

        private Stream Deskew(ref Stream stream)
        {
            Bitmap bmp = new Bitmap(stream);
            return ProcessDeskew(ref bmp);
        }

        private Stream ImageForOcr(ref Stream stream)
        {
            var st = ProcessImageForOcr(ref stream);
            return Deskew(ref st);
        }

        private Stream ProcessImageForOcr(ref Stream stream)
        {
            using Bitmap bmp = new Bitmap(stream);
            using var processedImage = RemoveNoiseAndSmooth(bmp);
            var ms = new MemoryStream();
            processedImage.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Tiff);
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
            CvInvoke.GaussianBlur(processedImage, processedImage, new Size(7, 7), 5, 5);

            processedImage = processedImage.Erode(1).Dilate(1);
            CvInvoke.Threshold(processedImage, processedImage, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);
            return processedImage;
        }

        private Image<Gray, byte> RemoveNoiseAndSmooth(Bitmap bmp)
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

        private double ConvertToRadians(double angle)
        {
            return (Math.PI / 180) * angle;
        }

        private double ConvertRadiansToDegrees(double radians)
        {
            return (180 / Math.PI) * radians;
        }
    }
}
