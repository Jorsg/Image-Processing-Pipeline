namespace az204_image_processor.Models
{
    public class ThumbnailSize
    {
        public string Suffix { get; set; } = string.Empty;  // e.g., "sm", "md", "lg"
        public int Width { get; set; }
        public int Height { get; set; }
        public int Quality { get; set; } = 75;
    }
    
}