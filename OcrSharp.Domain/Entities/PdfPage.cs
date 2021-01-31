using System.Collections.Generic;
using System.IO;

namespace OcrSharp.Domain.Entities
{
    public class PdfPage
    {
        public int PageNumber { get; private set; }
        public string Text { get; private set; }
        public ICollection<Stream> Images { get; private set; }

        public PdfPage(int pageNumber, string text)
        {
            PageNumber = pageNumber;
            Text = text;
            Images = new List<Stream>();
        }
    }
}
