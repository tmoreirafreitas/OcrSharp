using System;

namespace OcrSharp.Domain.Entities
{
    public class InMemoryFile : IDisposable
    {
        private bool disposedValue;

        public string FileName { get; set; }
        public int Page { get; set; }
        public double Accuracy { get; set; }
        public bool AppliedOcr { get; set; }
        public string RunTime { get; set; }
        public byte[] Content { get; set; }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    FileName = null;
                    RunTime = null;
                    Content = null;
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
