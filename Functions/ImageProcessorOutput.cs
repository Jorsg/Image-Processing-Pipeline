

using Microsoft.Azure.Functions.Worker;

namespace az204_image_processor.Functions
{
    public class ImageProcessorOutput
    {
        [BlobOutput("processed-images/{name}", Connection = "ImageStorageConnection")]
        public byte[]? ProcessedImage{get; set;}

        [BlobOutput("thumbnails/{name}", Connection = "ImageStorageConnection")]
        public byte[]? Thumbnail {get; set;}
    }
    
}