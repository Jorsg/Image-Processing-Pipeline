
using Models = az204_image_processor.Models;
using az204_image_processor.Services;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using System.Formats.Asn1;
using System.Security.AccessControl;

namespace az204_image_processor.Services
{
    public class ImageResizeService : IImageResizeService
    {
        private readonly ILogger<ImageResizeService> _logger;

        public ImageResizeService(ILogger<ImageResizeService> logger)
        {
            _logger = logger;
        }
        public async Task<byte[]> GenerateThumbnailAsync(Stream inputstream, int width = 150, int height = 150)
        {
            if (inputstream.CanSeek)
                inputstream.Position = 0;

            using var image = await Image.LoadAsync(inputstream);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Size = new Size(width, height),
                Position = AnchorPositionMode.Center
            }));

            _logger.LogInformation("🖼️ Thumbnail generated: {Width}x{Height}", image.Width, image.Height);

            using var outputStream = new MemoryStream();
            await image.SaveAsync(outputStream, new JpegEncoder { Quality = 75 });
            return outputStream.ToArray();
        }

        public async Task<Models.ImageInfo> GetImageInfoAsync(Stream inputStream)
        {
            if (inputStream.CanSeek)
                inputStream.Position = 0;

            var imageInfo = await Image.IdentifyAsync(inputStream);    
            return new Models.ImageInfo
            {
                OriginalWidth = imageInfo.Width,
                OriginalHeight = imageInfo.Height,
                Format = imageInfo.Metadata.DecodedImageFormat?.Name ?? "Unknown",
                FileSizeBytes = inputStream.Length
            };
        }

        public async Task<byte[]> ResizeImageAsync(Stream inputStream, int maxWidth, int maxHeight, int quality = 80)
        {
            using var image = await Image.LoadAsync(inputStream);
            _logger.LogInformation("📐 Original size: {Width}x{Height}", image.Width, image.Height);

            if (image.Width > maxWidth || image.Height > maxHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxWidth, maxHeight)
                }));

                _logger.LogInformation("📐 Resized to: {Width}x{Height}", image.Width, image.Height);
            }
            else
            {
                _logger.LogInformation("Image already within bounds, skipping resize");
            }

            using var outputStream = new MemoryStream();
            var encoder = GetEncoder(inputStream, quality);
            await image.SaveAsync(outputStream, encoder);
            return outputStream.ToArray();
        }



        private static IImageEncoder GetEncoder(Stream stream, int quality)
        {
            return new JpegEncoder { Quality = quality };
        }
    }
}