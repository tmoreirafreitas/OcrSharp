using System;
using System.Buffers;

namespace OcrSharp.Domain.Entities
{
    public class DocumentPage : IDisposable
    {
        private bool disposedValue;

        public int PageNumber { get; private set; }
        public string Content { get; private set; }
        public bool AppliedOcr { get; set; }
        public double Accuracy { get; set; }
        public string RunTime { get; set; }

        public DocumentPage(int pageNumber, string text, bool appliedOcr = false)
        {
            PageNumber = pageNumber;
            Content = text;
            AppliedOcr = appliedOcr;
        }

        public void ChangeContent(string text)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("filename can not be null or empty");
            Content = text;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    PageNumber = 0;
                    Accuracy = 0;
                    Content = null;
                    RunTime = null;
                    AppliedOcr = false;
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
