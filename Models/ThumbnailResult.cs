namespace az204_image_processor.Models
{
    public class ThumbnailResult
    {
        public string FileName { get; set; } = string.Empty;
        public string SizeSuffix { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }
        public int Height { get; set; }
        public long SizeBytes => Data.Length;
        public  string Contentype { get; set; } = "image/jpeg";
    }
    
}