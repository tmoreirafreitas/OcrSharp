using System.Collections.Generic;

namespace OcrSharp.Domain.Entities
{
    public class PdfFile
    {
        public string FileName { get; private set; }
        public int PagesNumber { get; private set; }
        public ICollection<PdfPage> Pages { get; private set; }

        public PdfFile(int pagesNumber, string fileName)
        {
            FileName = fileName;
            PagesNumber = pagesNumber;
            Pages = new List<PdfPage>();
        }
    }
}
