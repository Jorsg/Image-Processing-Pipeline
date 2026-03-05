
namespace az204_image_processor.Models
{
    public class ImageProcessingOptions
    {
        // Processed image limits
        public int MaxProcessedWidth { get; set; } = 1920;
        public int MaxProcessedHeight { get; set; } = 1080;
        public int ProcessedQuality { get; set; } = 80;

        // Thumbnail settings
        public int ThumbnailWidth { get; set; } = 150;
        public int ThumbnailHeight { get; set; } = 150;

        // Validation
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
        public string[] AllowedExtensions { get; set; } =
            { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
    }
    
}