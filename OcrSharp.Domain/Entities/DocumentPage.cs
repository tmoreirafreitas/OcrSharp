using System;

namespace OcrSharp.Domain.Entities
{
    public class DocumentPage
    {
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
    }
}
