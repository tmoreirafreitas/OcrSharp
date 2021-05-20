using System;
using System.Collections.Generic;

namespace OcrSharp.Domain.Entities
{
    public class DocumentFile : IDisposable
    {
        private bool disposedValue;

        public string FileName { get; private set; }
        public int PagesNumber { get; private set; }
        public TimeSpan RunTime { get; set; }
        public List<DocumentPage> Pages { get; private set; }

        public DocumentFile(int pagesNumber, string fileName)
        {
            FileName = fileName;
            PagesNumber = pagesNumber;
            Pages = new List<DocumentPage>();
        }

        public void ChangeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("filename can not be null or empty");
            FileName = fileName;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    foreach (var page in Pages)
                        page.Dispose();

                    Pages.Clear();
                    Pages = null;
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
