using System;

namespace OcrSharp.Domain.Entities
{
    public class PdfData : IDisposable
    {        
        public byte[] Binary { get; set; }
        public string FileName { get; set; }
        public string Headers { get; set; }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    FileName = null;
                    Headers = null;
                    Binary = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
