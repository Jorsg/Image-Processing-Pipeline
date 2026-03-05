using az204_image_processor.Models;

namespace az204_image_processor.Services
{
    public interface IBlobStorageService
    {
        Task UploadThumbnailsAsync(
            List<ThumbnailResult> thumbnails);
    }
}