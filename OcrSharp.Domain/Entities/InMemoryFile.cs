namespace OcrSharp.Domain.Entities
{
    public class InMemoryFile
    {
        public string FileName { get; set; }
        public int Page { get; set; }
        public double Accuracy { get; set; }
        public bool AppliedOcr { get; set; }
        public string RunTime { get; set; }
        public byte[] Content { get; set; }
    }
}
