using ClosedXML.Excel;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Microsoft.Extensions.Configuration;
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

        public OcrFileService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<InMemoryFile> ApplyOcrAsync(InMemoryFile inMemory, bool bestOcuracy = false)
        {           
            var file = await ApplyOcrAsync(inMemory.Content.ArrayToStream(), bestOcuracy);
            file.FileName = $"{Path.GetFileNameWithoutExtension(inMemory.FileName)}.txt";           
            return file;
        }

        public async Task<InMemoryFile> ApplyOcrAsync(Stream stream, bool bestOcuracy = false)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var stResult = ImageForOcr(ref stream);
            var file = await ProcessOcrAsync(stResult, bestOcuracy);

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
            file.RunTime = elapsedTime;
            return file;
        }

        private async Task<InMemoryFile> ProcessOcrAsync(Stream stream, bool bestOcuracy = false)
        {
            string tessDataPath = bestOcuracy ? _configuration["Tesseract:tessDataBest"] : _configuration["Tesseract:tessDataFast"];

            using (var engine = new TesseractEngine(tessDataPath, "por", EngineMode.Default))
            {
                using (var image = Pix.LoadFromMemory(await stream.StreamToArrayAsync()))
                using (var page = engine.Process(image, PageSegMode.AutoOsd))
                {
                    var ocrResult = Regex.Replace(page.GetText(), @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
                    return new InMemoryFile
                    {
                        Content = Encoding.UTF8.GetBytes(ocrResult),
                        Accuracy = page.GetMeanConfidence(),
                        AppliedOcr = true
                    };
                }
            }
        }

        private Stream ProcessDeskew(ref Bitmap bitmap)
        {
            using (var img = bitmap.ToImage<Gray, byte>())
            using (var gray = img.ThresholdBinaryInv(new Gray(200), new Gray(255)).Dilate(5))
            {
                using (VectorOfPoint points = new VectorOfPoint())
                {
                    CvInvoke.FindNonZero(gray, points);
                    var minareaRect = CvInvoke.MinAreaRect(points);
                    using (var rotationMatrix = new Mat(new Size(2, 3), DepthType.Cv32F, 1))
                    {
                        using (var rotatedImage = img.CopyBlank())
                        {
                            if (minareaRect.Angle < -45)
                            {
                                minareaRect.Angle = 90 + minareaRect.Angle;
                            }

                            CvInvoke.GetRotationMatrix2D(minareaRect.Center, minareaRect.Angle, 1.0, rotationMatrix);
                            CvInvoke.WarpAffine(img, rotatedImage, rotationMatrix, img.Size, Inter.Cubic, borderMode: BorderType.Replicate);

                            var stream = new MemoryStream();
                            rotatedImage.Convert<Bgr, byte>().ToBitmap().Save(stream, System.Drawing.Imaging.ImageFormat.Tiff);
                            stream.Position = 0;
                            return stream;
                        }
                    }
                }
            }            
        }

        private Stream Deskew(ref Stream stream)
        {
            Bitmap bmp = new Bitmap(stream);
            return ProcessDeskew(ref bmp);
        }
        private Stream Deskew(string fileName)
        {
            Bitmap bmp = new Bitmap(fileName);
            return ProcessDeskew(ref bmp);
        }

        private Stream ImageForOcr(ref Stream stream)
        {
            var st = ProcessImageForOcr(ref stream);
            return Deskew(ref st);
        }

        private Stream ProcessImageForOcr(ref Stream stream)
        {
            Image<Gray, byte> processedImage = null;
            using (Bitmap bmp = new Bitmap(stream))
                processedImage = RemoveNoiseAndSmooth(bmp);

            var ms = new MemoryStream();
            processedImage.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Tiff);
            ms.Position = 0;
            processedImage.Dispose();
            return ms;
        }

        private Image<Gray, byte> ImageSmoothening(ref Image<Gray, byte> image)
        {
            const int binaryThreshold = 127;
            var processedImage = image.CopyBlank();

            CvInvoke.Threshold(image, processedImage, binaryThreshold, 255, ThresholdType.Binary);
            CvInvoke.Threshold(processedImage, processedImage, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);
            CvInvoke.GaussianBlur(processedImage, processedImage, new Size(1, 1), 0);
            CvInvoke.Threshold(processedImage, processedImage, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);
            return processedImage;
        }

        private Image<Gray, byte> RemoveNoiseAndSmooth(Bitmap bmp)
        {
            const int imageSize = 1800;
            Image<Gray, byte> image = bmp.ToImage<Gray, byte>();
            var factor = Math.Max(1, imageSize / image.Width);
            image.Resize(image.Width * factor, image.Height * factor, Inter.Cubic, true);

            var filtered = image.CopyBlank();
            var opening = image.CopyBlank();
            var closing = image.CopyBlank();

            //CvInvoke.AdaptiveThreshold(image, filtered, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, 85, 11);           

            CvInvoke.AdaptiveThreshold(image, filtered, 255, AdaptiveThresholdType.MeanC, ThresholdType.Binary, 41, 3);
            var kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(1, 1), new Point(-1, -1));
            CvInvoke.MorphologyEx(filtered, opening, MorphOp.Open, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(1.0));
            CvInvoke.MorphologyEx(opening, closing, MorphOp.Close, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(1.0));

            var imageSmoothening = ImageSmoothening(ref closing);
            var orImage = image.CopyBlank();
            CvInvoke.BitwiseOr(imageSmoothening, closing, orImage);
            return orImage;
        }

        public InMemoryFile TextDetectionAndRecognitionToConvertTables(string fullFileName, int NoCols = 4, 
            float MorphThrehold = 30f, int binaryThreshold = 200, int offset = 5, double factor = 2.5, bool bestOcuracy = false)
        {
            Deskew(fullFileName);
            InMemoryFile file = null;
            using (var bmp = new Bitmap(fullFileName))
            {
                using (var image = bmp.ToImage<Gray, byte>()
                     .Resize(factor, Inter.Cubic)
                     .ThresholdBinaryInv(new Gray(binaryThreshold), new Gray(255)))
                {
                    int length = (int)(image.Width * MorphThrehold / 100);

                    Mat vProfile = new Mat();
                    Mat hProfile = new Mat();

                    var kernelV = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(1, length), new Point(-1, -1));
                    var kernelH = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(length, 1), new Point(-1, -1));

                    CvInvoke.Erode(image, vProfile, kernelV, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(255));
                    CvInvoke.Dilate(vProfile, vProfile, kernelV, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(255));

                    CvInvoke.Erode(image, hProfile, kernelH, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(255));
                    CvInvoke.Dilate(hProfile, hProfile, kernelH, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(255));

                    var mergedImage = vProfile.ToImage<Gray, byte>().Or(hProfile.ToImage<Gray, byte>());
                    mergedImage._ThresholdBinary(new Gray(1), new Gray(255));

                    VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
                    Mat h = new Mat();

                    CvInvoke.FindContours(mergedImage, contours, h, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                    int bigCID = GetBiggestContourID(contours);
                    var bbox = CvInvoke.BoundingRectangle(contours[bigCID]);

                    mergedImage.ROI = bbox;
                    image.ROI = bbox;
                    var temp = mergedImage.Copy();
                    temp._Not();

                    var imgTable = image.Copy();
                    contours.Clear();

                    CvInvoke.FindContours(temp, contours, h, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                    var filtercontours = FilterContours(contours, 500);
                    var bboxList = Contours2BBox(filtercontours);
                    var sortedBBoxes = bboxList.OrderBy(x => x.Y).ThenBy(y => y.X).ToList();

                    var folder = Path.GetDirectoryName(fullFileName);
                    imgTable.Save(Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(fullFileName)}_processed.tiff"));
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Outputs");

                        int rowCounter = 1;
                        char colCounter = 'A';

                        for (int i = 0; i < sortedBBoxes.Count; i++)
                        {
                            var rect = sortedBBoxes[i];
                            rect.X += offset;
                            rect.Y += offset;
                            rect.Width -= offset;
                            rect.Height -= offset;

                            imgTable.ROI = rect;

                            string tessDataPath = bestOcuracy ? _configuration["Tesseract:tessDataBest"] : _configuration["Tesseract:tessDataFast"];
                            using (var engine = new TesseractEngine(tessDataPath, "por+eng", EngineMode.TesseractAndLstm))
                            {
                                engine.DefaultPageSegMode = PageSegMode.SingleBlock;
                                using (var page = engine.Process(Pix.LoadFromMemory(imgTable.Copy().ToJpegData())))
                                {
                                    byte[] bytes = Encoding.Default.GetBytes(page.GetText());
                                    var text = Encoding.UTF8.GetString(bytes).Replace("\r\n", "");

                                    if (i % NoCols == 0)
                                    {
                                        if (i > 0)
                                        {
                                            rowCounter++;
                                        }
                                        colCounter = 'A';
                                        worksheet.Cell(colCounter.ToString() + rowCounter.ToString()).Value = text;
                                    }
                                    else
                                    {
                                        colCounter++;
                                        worksheet.Cell(colCounter + rowCounter.ToString()).Value = text;
                                    }
                                    imgTable.ROI = Rectangle.Empty;
                                }
                            }
                        }

                        using (var stream = new MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            stream.Position = 0;
                            file = new InMemoryFile
                            {
                                FileName = $@"OcrTableToText_{Path.GetFileNameWithoutExtension(fullFileName)}_{DateTime.Now.GetDateNowEngFormat()}.xlsx",
                                Content = stream.ToArray()
                            };
                        }
                    }
                }
            }

            return file;
        }

        private int GetBiggestContourID(VectorOfVectorOfPoint contours)
        {
            double maxArea = double.MaxValue * (-1);
            int contourId = -1;
            for (int i = 0; i < contours.Size; i++)
            {
                double area = CvInvoke.ContourArea(contours[i]);
                if (area > maxArea)
                {
                    maxArea = area;
                    contourId = i;
                }
            }
            return contourId;
        }

        private VectorOfVectorOfPoint FilterContours(VectorOfVectorOfPoint contours, double threshold = 50)
        {
            VectorOfVectorOfPoint filteredContours = new VectorOfVectorOfPoint();
            for (int i = 0; i < contours.Size; i++)
            {
                if (CvInvoke.ContourArea(contours[i]) >= threshold)
                {
                    filteredContours.Push(contours[i]);
                }
            }

            return filteredContours;
        }

        private List<Rectangle> Contours2BBox(VectorOfVectorOfPoint contours)
        {
            List<Rectangle> list = new List<Rectangle>();
            for (int i = 0; i < contours.Size; i++)
            {
                list.Add(CvInvoke.BoundingRectangle(contours[i]));
            }

            return list;
        }
    }
}
