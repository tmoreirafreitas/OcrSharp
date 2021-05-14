namespace OcrSharp.Domain.Options
{
    public class TesseractOptions
    {
        public string Oem { get; set; }
        public string Psm { get; set; }
        public string Language { get; set; }
        public string TesseractExe { get; set; }
        public string ThreadLimit { get; set; }
        public string Dpi { get; set; }
    }
}
