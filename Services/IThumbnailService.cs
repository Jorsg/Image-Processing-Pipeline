using az204_image_processor.Models;

namespace az204_image_processor.Services
{
    public interface IThumbnailService
    {
        Task<List<ThumbnailResult>> GenerateThumbnailsAsync(Stream inputStream, string originalFileName);
    }
    
}