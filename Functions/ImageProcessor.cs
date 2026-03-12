using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using az204_image_processor.Services;
using az204_image_processor.Models;
using Microsoft.Extensions.Options;

namespace az204_image_processor.Functions
{
    public class ImageProcessor
    {
        private readonly ILogger<ImageProcessor> _logger;
        private readonly IImageResizeService _resizeService;
        private readonly ImageProcessingOptions _options;
        private readonly IThumbnailService _thumbnailService;
        private readonly IBlobStorageService _blobService;
        private readonly IComputerVisionService _visionService;


        public ImageProcessor(ILogger<ImageProcessor> logger,
        IImageResizeService resizeService,
        IOptions<ImageProcessingOptions> options,
        IBlobStorageService blobService,
        IThumbnailService thumbnailService, IComputerVisionService visionService)
        {
            _logger = logger;
            _options = options.Value;
            _resizeService = resizeService;
            _blobService = blobService;
            _thumbnailService = thumbnailService;
            _visionService = visionService;
        }

        [Function("ImageProcessor")]
        [BlobOutput("processed-image/{name}", Connection = "ImageStorageConnection")]
        public async Task<ImageProcessorOutput> Run(
            [BlobTrigger("raw-images/{name}", Connection = "ImageStorageConnection")] Stream inputBlob,
            string name, FunctionContext context)
        {
            var startTime = DateTime.UtcNow;
            var output = new ImageProcessorOutput();

            _logger.LogInformation("🖼️ Processing blob: {Name} | Size: {Size} bytes", name, inputBlob.Length);

            try
            {
                if (!IsvalidImage(name))
                {
                    _logger.LogWarning($"Skipping non-image file: {name}");
                    return output;
                }

                if (inputBlob.Length > _options.MaxFileSizeBytes)
                {
                    _logger.LogWarning($"File too large: {name} ({inputBlob.Length / (1024.0 * 1024.0):F2} MB) ");
                    await MoveToPoisonContainerAsync(name, inputBlob);
                    return output;
                }

                // ── Extract metadata (for Table Storage in Step 5) ──
                var imageInfo = await _resizeService
                    .GetImageInfoAsync(inputBlob);

                _logger.LogInformation(
                    "📊 Image info: {W}x{H} | Format: {F}",
                    imageInfo.OriginalWidth,
                    imageInfo.OriginalHeight,
                    imageInfo.Format);

                // ── Resize: processed image ──
                if (inputBlob.CanSeek) inputBlob.Position = 0;

                output.ProcessedImage = await _resizeService
                    .ResizeImageAsync(
                        inputBlob,
                        _options.MaxProcessedWidth,
                        _options.MaxProcessedHeight,
                        _options.ProcessedQuality);

                _logger.LogInformation(
                     "✅ Processed image: {Original} → {Processed} bytes",
                     inputBlob.Length, output.ProcessedImage.Length);

                // ── Resize: thumbnail ──
                if (inputBlob.CanSeek) inputBlob.Position = 0;

                var thumbnails = await _thumbnailService
                      .GenerateThumbnailsAsync(inputBlob, name);

                await _blobService.UploadThumbnailsAsync(thumbnails);


                // ──────────────────────────────────────
                // Step 4 will add: Computer Vision API
                // Step 5 will add: Table Storage metadata
                // ──────────────────────────────────────

                if (inputBlob.CanSeek) inputBlob.Position = 0;

                var visionResult = await _visionService.FullAnalysisAsync(inputBlob);

                if (visionResult.Success)
                {
                    _logger.LogInformation(
                       "Vision: \"{Desc}\" | Tags: [{Tags}] | " +
                       "Text found: {HasText} | " +
                       "Retries: {Retries} | API time: {Time}ms",
                       visionResult.Description,
                       string.Join(", ",
                           visionResult.Tags
                               .Take(5)
                               .Select(t => $"{t.Name}({t.Confidence:P0})")),
                       !string.IsNullOrEmpty(visionResult.ExtractedText),
                       visionResult.RetryCount,
                       visionResult.ApiCalDurationMs);

                    if (!string.IsNullOrEmpty(visionResult.ExtractedText))
                    {
                        _logger.LogInformation(
                               "Extracted text preview: {Text}",
                               visionResult.ExtractedText.Length > 200
                                   ? visionResult.ExtractedText[..200] + "..."
                                   : visionResult.ExtractedText);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        $"Vision analysis failed: {visionResult.ErrorMessage}");
                    // Don't fail the whole function — images without
                    // vision results are still valid
                }


                var processingTime = DateTime.UtcNow - startTime;

                _logger.LogInformation(
                     $"Total: {name} completed in {processingTime.TotalMilliseconds}ms | " +
                     $"Processed: {output.ProcessedImage.Length} bytes | Thumbnails: {thumbnails.Count}");
                return output;

            }
            catch (System.Exception ex)
            {

                _logger.LogError(ex, $" Failed to process blob: {name}");
                throw;
            }
        }


        private bool IsvalidImage(string fileName)
        {
            var extension = Path.GetExtension(fileName)
                .ToLowerInvariant();
            return _options.AllowedExtensions.Contains(extension);
        }

        private async Task MoveToPoisonContainerAsync(string blobName, Stream data)
        {
            var connectionString = Environment.GetEnvironmentVariable("ImageStorageConnection");
            var containerName = Environment.GetEnvironmentVariable("PoisonContainer") ?? "poison-images";

            var blobClient = new BlobServiceClient(connectionString)
            .GetBlobContainerClient(containerName)
            .GetBlobClient(blobName);

            if (data.CanSeek) data.Position = 0;

            await blobClient.UploadAsync(data, new BlobUploadOptions
            {
                Metadata = new Dictionary<string, string>
               {
                   {"FailureReason", "FileToolarge"},
                   {"OriginalContainer","raw-images"},
                   {"FaileAt",DateTime.UtcNow.ToString("o")}
               }
            });
            _logger.LogWarning($"Moved {blobName} to poison container");
        }
    }
}