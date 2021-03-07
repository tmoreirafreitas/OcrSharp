namespace OcrSharp.Domain.Entities
{
    public class InMemoryFile
    {
        public string FileName { get; set; }
        public float Accuracy { get; set; }
        public bool AppliedOcr { get; set; }
        public string RunTime { get; set; }
        public byte[] Content { get; set; }
    }
}
