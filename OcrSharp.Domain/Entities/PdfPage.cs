using System;

namespace OcrSharp.Domain.Entities
{
    public class PdfPage
    {
        public int PageNumber { get; private set; }
        public string Content { get; private set; }

        public PdfPage(int pageNumber, string text)
        {
            PageNumber = pageNumber;
            Content = text;
        }

        public void ChangeContent(string text)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("filename can not be null or empty");
            Content = text;
        }
    }
}
