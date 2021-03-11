using System;
using System.Collections.Generic;

namespace OcrSharp.Domain.Entities
{
    public class DocumentFile
    {
        public string FileName { get; private set; }
        public int PagesNumber { get; private set; }
        public string RunTimeTotal { get; set; }
        public ICollection<DocumentPage> Pages { get; private set; }        

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
    }
}
