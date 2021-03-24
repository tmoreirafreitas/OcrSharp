namespace OcrSharp.Api.ViewModel
{
    public class OcrResultViewModel
    {
        public string ContentType { get; set; }
        public string FileName { get; set; }
        public int CurrentPage { get; set; }
        public int NumberOfPages { get; set; }
        public double Accuracy { get; set; }
        public string RunTime { get; set; }
        public bool IsBase64 { get; set; }
        public string Content { get; set; }
    }
}
