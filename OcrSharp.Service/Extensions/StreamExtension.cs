using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OcrSharp.Service.Extensions
{
    public static class StreamExtension
    {
        public static async Task<byte[]> StreamToArrayAsync(this Stream stream, CancellationToken cancellation = default(CancellationToken))
        {
            if (stream == null || stream.Length == 0)
                throw new Exception("stream can not be null or empty");

            var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellation);
            ms.Position = 0;
            return ms.ToArray();
        }

        public static Stream ArrayToStream(this byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new Exception("Array of bytes can not be null or empty");

            var ms = new MemoryStream(data)
            {
                Position = 0
            };
            return ms;
        }
    }
}
