using az204_image_processor.Models;
namespace az204_image_processor.Services
{
    public interface IImageResizeService
    {
        Task<byte[]> ResizeImageAsync(
            Stream inputStream,
            int maxWidth,
            int maxHeight,
            int quality = 80);
        Task<byte[]> GenerateThumbnailAsync(
            Stream inputstream,
            int width = 150,
            int height = 150 );

        Task<ImageInfo> GetImageInfoAsync(Stream inputStream);        

    }

}