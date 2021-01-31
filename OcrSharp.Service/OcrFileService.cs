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
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
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

        public async Task<InMemoryFile> ApplyOcrAsync(string fullFileName)
        {
            await ProcessImageForOcr(fullFileName);

            string tessDataPath = _configuration["Tesseract:tessDataFolder"];
            using (var engine = new TesseractEngine(tessDataPath, "por+eng", EngineMode.Default))
            {
                var image = Pix.LoadFromFile(fullFileName);
                using (var page = engine.Process(image, PageSegMode.AutoOsd))
                {
                    var ocrResult = Regex.Replace(page.GetText(), @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);

                    return new InMemoryFile
                    {
                        FileName = $"{Path.GetFileNameWithoutExtension(fullFileName)}.txt",
                        Content = Encoding.UTF8.GetBytes(ocrResult)
                    };
                }
            }
        }

        private Task DeskewAsync(string fileName, double maxSkew = 2)
        {
            Image<Gray, byte> rotatedImage = null;
            using (Bitmap bmp = new Bitmap(fileName))
            {
                using (var image = bmp.ToImage<Gray, byte>())
                {
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
                        wearableAngles = allAngles.Where(angle => ToRadian(90 - maxSkew) < Math.Abs(angle) && Math.Abs(angle) < ToRadian(90 + maxSkew)).ToList();
                    }
                    else
                    {
                        wearableAngles = allAngles.Where(angle => Math.Abs(angle) < ToRadian(maxSkew)).ToList();
                    }

                    var angle = 0.0;
                    if (wearableAngles.Count > 5)
                    {
                        angle = ToDegree(wearableAngles.GetMedian());
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

                    using (var rotationMatrix = new Mat(new Size(2, 3), DepthType.Cv32F, 1))
                    {
                        CvInvoke.GetRotationMatrix2D(new PointF(image.Width / 2, image.Height / 2), angle, 1.0, rotationMatrix);
                        CvInvoke.WarpAffine(image, rotatedImage, rotationMatrix, image.Size, Inter.Cubic, borderMode: BorderType.Replicate);
                    }
                }
            }

            File.Delete(fileName);
            rotatedImage.Convert<Bgr, byte>().Save(fileName);
            return Task.CompletedTask;
        }

        private async Task ProcessImageForOcr(string fileName)
        {
            await DeskewAsync(fileName);

            Image<Gray, byte> processedImage = null;
            using (Bitmap bmp = new Bitmap(fileName))
                processedImage = RemoveNoiseAndSmooth(bmp);

            if (processedImage != null)
            {
                File.Delete(fileName);
                processedImage.Save(fileName);
            }
        }

        private Image<Gray, byte> ImageSmoothening(Image<Gray, byte> image)
        {
            const int binaryThresold = 127;
            var processedImage = image.CopyBlank();

            CvInvoke.Threshold(image, processedImage, binaryThresold, 255, ThresholdType.Binary);
            CvInvoke.Threshold(processedImage, processedImage, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);
            CvInvoke.GaussianBlur(processedImage, processedImage, new Size(3, 3), 0);
            CvInvoke.Threshold(processedImage, processedImage, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);
            return processedImage;
        }

        private Image<Gray, byte> RemoveNoiseAndSmooth(Bitmap bmp, double factor = 1.3)
        {
            Image<Gray, byte> image = bmp.ToImage<Gray, byte>();
            image = image.Resize(factor, Inter.Cubic);

            var filtered = image.CopyBlank();
            var opening = image.CopyBlank();
            var closing = image.CopyBlank();

            CvInvoke.AdaptiveThreshold(image, filtered, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, 85, 11);

            var kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(3, 3), new Point(-1, -1));
            CvInvoke.MorphologyEx(filtered, opening, MorphOp.Open, kernel, new Point(-1, -1), 1, BorderType.Constant, new MCvScalar(1.0));
            CvInvoke.MorphologyEx(opening, closing, MorphOp.Close, kernel, new Point(-1, -1), 1, BorderType.Constant, new MCvScalar(1.0));

            var imageSmoothening = ImageSmoothening(closing);
            var orImage = image.CopyBlank();
            CvInvoke.BitwiseOr(imageSmoothening, closing, orImage);
            return orImage;
        }

        private double ToRadian(double degree)
        {
            return (Math.PI / 180) * degree;
        }

        private double ToDegree(double radian)
        {
            return (180 / Math.PI) * radian;
        }    

        public async Task<InMemoryFile> TextDetectionAndRecognitionToConvertTables(string fullFileName, int NoCols = 4, float MorphThrehold = 30f, int binaryThreshold = 200, int offset = 5, double factor = 2.5)
        {
            await DeskewAsync(fullFileName);
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
                    imgTable.Save(Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(fullFileName)}_processed.png"));
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

                            string tessDataPath = DownloadAndExtractLanguagePack();
                            using (var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.TesseractOnly))
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

        private static string DownloadAndExtractLanguagePack()
        {
            //Source path to the zip file
            string langPackPath = "https://github.com/tesseract-ocr/tessdata/archive/3.04.00.zip";

            string zipFileName = AppDomain.CurrentDomain.BaseDirectory + "\\tessdata.zip";
            string tessDataFolder = AppDomain.CurrentDomain.BaseDirectory + "\\tessdata";

            //Check and download the source file
            if (!File.Exists(zipFileName))
            {
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(langPackPath, zipFileName);
                }
            }

            //Extract the zip to tessdata folder
            if (!Directory.Exists(tessDataFolder))
            {
                ZipFile.ExtractToDirectory(zipFileName, AppDomain.CurrentDomain.BaseDirectory);
                var extractedDir = Directory.EnumerateDirectories(AppDomain.CurrentDomain.BaseDirectory).FirstOrDefault(x => x.Contains("tessdata"));
                Directory.Move(extractedDir, tessDataFolder);
            }

            return tessDataFolder;
        }
    }
}
