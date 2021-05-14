using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using OcrSharp.Domain.Interfaces.Services;
using System;
using System.Drawing;
using System.Threading.Tasks;

namespace OcrSharp.Service
{
    /// <summary>
    /// Image processing service using the EmguCV engine.
    /// </summary>
    public class OpenCvService : IOpenCvService
    {
        /// <summary>
        /// Method used for image smoothing
        /// </summary>
        /// <param name="image">An bitmap type image to method</param>
        /// <returns>Returns a new smoothed image.</returns>
        public Task<Bitmap> ImageSmootheningAsync(Bitmap image)
        {
            using (image)
            {
                using var src = image.ToImage<Gray, byte>();
                const int binaryThreshold = 127;
                var processedImage = src.CopyBlank();

                CvInvoke.Threshold(src, processedImage, binaryThreshold, 255, ThresholdType.Binary);
                CvInvoke.Threshold(processedImage, processedImage, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);
                CvInvoke.GaussianBlur(processedImage, processedImage, new Size(1, 1), 0);

                //processedImage = processedImage.Erode(1).Dilate(1);
                CvInvoke.Threshold(processedImage, processedImage, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);
                return Task.FromResult(processedImage.ToBitmap());
            }
        }

        /// <summary>
        /// Method used to deskew the image.
        /// </summary>
        /// <param name="image">An bitmap type image to method</param>
        /// <returns>Returns a new deskewed image</returns>
        public Task<Bitmap> DeskewAsync(Bitmap image)
        {
            using (image)
            {
                Bitmap tempImage;
                if (image.PixelFormat.ToString().Equals("Format8bppIndexed"))
                {
                    tempImage = image;
                }
                else
                {
                    tempImage = AForge.Imaging.Filters.Grayscale.CommonAlgorithms.BT709.Apply(image);
                }

                AForge.Imaging.DocumentSkewChecker skewChecker = new AForge.Imaging.DocumentSkewChecker();
                // get documents skew angle
                double angle = skewChecker.GetSkewAngle(tempImage);
                // create rotation filter
                AForge.Imaging.Filters.RotateBilinear rotationFilter = new AForge.Imaging.Filters.RotateBilinear(-angle);
                rotationFilter.FillColor = Color.Black;
                // rotate image applying the filter
                Bitmap rotatedImage = rotationFilter.Apply(tempImage);

                return Task.FromResult(rotatedImage);
            }
        }

        /// <summary>
        /// Method used to remove noise and smooth the image
        /// </summary>
        /// <param name="image">An bitmap type image to method</param>
        /// <returns>Returns a new, smoothed, noise-free image</returns>
        public async Task<Bitmap> RemoveNoiseAndSmoothAsync(Bitmap image)
        {
            using (image)
            {
                const int imageSize = 1800;
                Image<Gray, byte> tempImage = image.ToImage<Gray, byte>();
                var factor = Math.Max(1, imageSize / tempImage.Width);
                tempImage = tempImage.Resize(tempImage.Width * factor, tempImage.Height * factor, Inter.Cubic, true);

                using var filtered = tempImage.CopyBlank();
                using var opening = tempImage.CopyBlank();
                using var closing = tempImage.CopyBlank();
                CvInvoke.AdaptiveThreshold(tempImage, filtered, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, 85, 11);
                //CvInvoke.AdaptiveThreshold(image, filtered, 255, AdaptiveThresholdType.MeanC, ThresholdType.Binary, 41, 3);

                using var kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(1, 1), new Point(-1, -1));
                CvInvoke.MorphologyEx(filtered, opening, MorphOp.Open, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(1.0));
                CvInvoke.MorphologyEx(opening, closing, MorphOp.Close, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(1.0));

                using var imageSmoothening = await ImageSmootheningAsync(closing.ToBitmap());
                var orImage = tempImage.CopyBlank();
                CvInvoke.BitwiseOr(imageSmoothening.ToImage<Gray, byte>(), closing, orImage);
                tempImage.Dispose();
                tempImage = null;
                return orImage.ToBitmap();
            }
        }
    }
}
