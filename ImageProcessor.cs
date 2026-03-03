using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Google.Protobuf;

namespace az204_image_processor
{
    public class ImageProcessor
    {
        private readonly ILogger<ImageProcessor> _logger;

        public ImageProcessor(ILogger<ImageProcessor> logger)
        {
            _logger = logger;
        }

        [Function("ImageProcessor")]
        [BlobOutput("processed-image/{name}", Connection = "ImageStorageConnection")]
        public async Task<byte[]?> Run(
            [BlobTrigger("raw-images/{name}", Connection = "ImageStorageConnection")] Stream inputBlob,
            string name, FunctionContext context)
        {
            var startTime = DateTime.UtcNow;

            _logger.LogInformation($"Processing blob: {name} | Size: {inputBlob.Length}");

            try
            {
                if (!IsvalidImage(name))
                {
                    _logger.LogWarning($"Skipping non-image file: {name}");
                    return null;
                }

                using var memoryStream = new MemoryStream();
                await inputBlob.CopyToAsync(memoryStream);
                var imageByte = memoryStream.ToArray();

                if(imageByte.Length > 10 * 1024 * 1024)
                {
                     _logger.LogWarning($"File too large: {name} ({imageByte.Length} MB) / (1024.0 * 1024.0)");
                    await MoveToPoisonContainerAsync(name, imageByte);
                    return null;
                }

                _logger.LogInformation($"Blob {name} read successfuly Size: {imageByte.Length} byte");

                    // ──────────────────────────────────────
                // Steps 3-5 will add:
                //   - Computer Vision API call (with retry)
                //   - Thumbnail generation
                //   - Table Storage metadata write
                // ──────────────────────────────────────

                var processingTime = DateTime.UtcNow - startTime;
                _logger.LogInformation($"Processing completed for {name} in {processingTime.TotalMilliseconds}ms");

                // Output binding writes this to processed-images/{name}
                return imageByte;

            }
            catch (System.Exception ex)
            {

                 _logger.LogError(ex,$" Failed to process blob: {name}");

                // Pro Tip: The poisonBlobThreshold in host.json handles
                // automatic poison detection after 3 failures.
                // We rethrow to let the runtime track the failure count.
                throw;;
            }
        }


        private static bool IsvalidImage(string fileName)
        {
            var validExtensions = new[]
            {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
            };
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return Array.Exists(validExtensions, ext => ext == extension);
        }

        private async Task MoveToPoisonContainerAsync(string blobName, byte[] datea)
        {
            var connectionString = Environment.GetEnvironmentVariable("ImageStorageConnection");
            var containerName = Environment.GetEnvironmentVariable("PoisonContainer") ?? "poison-images";

            var blobClient = new BlobServiceClient(connectionString)
            .GetBlobContainerClient(containerName)
            .GetBlobClient(blobName);

            using var Stream = new MemoryStream(datea);
            await blobClient.UploadAsync(Stream, new BlobUploadOptions
            {
               HttpHeaders = new BlobHttpHeaders
               {
                 ContentType = "application/octet-stream"  
               }, 
               Metadata = new Dictionary<string, string>
               {
                   {"FailureReason", "FileToolager"},
                   {"OriginalContainer","raw-images"},
                   {"FaileAt",DateTime.UtcNow.ToString("0")}
               }
            });
            _logger.LogWarning($"Moved {blobName} to poison container");
        }
    }
}