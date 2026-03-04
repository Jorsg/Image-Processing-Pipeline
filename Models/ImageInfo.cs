namespace az204_image_processor.Models
{
    
    public class ImageInfo
    {
         public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }
        public string Format { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
    }
}