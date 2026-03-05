using az204_image_processor.Models;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace az204_image_processor.Services
{
    public class ThumbnailService : IThumbnailService
    {

        private readonly ILogger<ThumbnailService> _logger;
        private readonly ThumbnailOptions _options;

        public ThumbnailService(ILogger<ThumbnailService> logger, ThumbnailOptions options)
        {
            _logger = logger;
            _options = options;
        }

        public async Task<List<ThumbnailResult>> GenerateThumbnailsAsync(Stream inputStream, string originalFileName)
        {
            var results = new List<ThumbnailResult>();
            var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
            var baseName = Path.GetFileNameWithoutExtension(originalFileName);

            if (inputStream.CanSeek)
                inputStream.Position = 0;


            using var originalImage = await Image.LoadAsync(inputStream);

            _logger.LogInformation($"Generating {_options.Sizes.Length} thumbnails for: {originalFileName} {originalImage.Width} X {originalImage.Height}");

            foreach (var sizeConfig in _options.Sizes)
            {
                using var clone = originalImage.Clone(ctx =>
                {
                    ctx.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Crop,
                        Size = new Size(sizeConfig.Width, sizeConfig.Height),
                        Position = AnchorPositionMode.Center
                    });
                });

                using var outputStream = new MemoryStream();
                var Contentype = await EncodeImageAsync(clone, outputStream, extension, sizeConfig.Quality);

                // Build organized file name: photo_sm.jpg, photo_md.jpg
                var thumbFileName =
                    $"{baseName}_{sizeConfig.Suffix}{extension}";

                results.Add(new ThumbnailResult
                {
                    FileName = thumbFileName,
                    SizeSuffix = sizeConfig.Suffix,
                    Data = outputStream.ToArray(),
                    Width = clone.Width,
                    Height = clone.Height,
                    Contentype = Contentype
                });

                _logger.LogInformation($" {sizeConfig.Suffix}: {clone.Width}x{clone.Height} | {outputStream.Length} bytes",
                    sizeConfig.Suffix, clone.Width, clone.Height,
                    outputStream.Length);
            }
            return results;
        }


        private async Task<string> EncodeImageAsync(Image image, MemoryStream output,
         string extension, int quality)
        {
            switch (extension)
            {
                case ".png":
                    await image.SaveAsync(output, new PngEncoder
                    {
                        CompressionLevel = PngCompressionLevel.BestSpeed,
                        ColorType = PngColorType.RgbWithAlpha
                    });
                    return "image/png";
                case ".gif":
                    await image.SaveAsync(output, new GifEncoder());
                    return "image/gif";

                case ".webp":
                    await image.SaveAsync(output, new WebpEncoder
                    {
                        Quality = quality
                    });
                    return "image/webp";

                default: // .jpg, .jpeg, .bmp → JPEG output
                    await image.SaveAsync(output, new JpegEncoder
                    {
                        Quality = quality
                    });
                    return "image/jpeg";
            }

        }
    }

}