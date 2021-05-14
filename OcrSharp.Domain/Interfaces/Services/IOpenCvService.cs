using System.Drawing;
using System.Threading.Tasks;


namespace OcrSharp.Domain.Interfaces.Services
{
    /// <summary>
    /// Image processing service using the EmguCV engine.
    /// </summary>
    public interface IOpenCvService : IDomainService
    {
        /// <summary>
        /// Method used to deskew the image.
        /// </summary>
        /// <param name="image">An bitmap type image to method</param>
        /// <returns>Returns a new deskewed image</returns>
        Task<Bitmap> DeskewAsync(Bitmap image);
        /// <summary>
        /// Method used to remove noise and smooth the image
        /// </summary>
        /// <param name="image">An bitmap type image to method</param>
        /// <returns>Returns a new, smoothed, noise-free image</returns>
        Task<Bitmap> RemoveNoiseAndSmoothAsync(Bitmap image);
        /// <summary>
        /// Method used for image smoothing
        /// </summary>
        /// <param name="image">An bitmap type image to method</param>
        /// <returns>Returns a new smoothed image.</returns>
        Task<Bitmap> ImageSmootheningAsync(Bitmap image);
    }
}
