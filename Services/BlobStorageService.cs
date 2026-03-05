using az204_image_processor.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace az204_image_processor.Services
{
    public class BlobStorageService : IBlobStorageService
    {

        private readonly ILogger<BlobStorageService> _logger;
        private readonly BlobServiceClient _blobServiceClient;

        public BlobStorageService(ILogger<BlobStorageService> logger, BlobServiceClient blobServiceClient)
        {
            _logger = logger;
            _blobServiceClient = blobServiceClient;
        }

        public async Task UploadThumbnailsAsync(List<ThumbnailResult> thumbnails)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient("thumbnails");

            var uploadTasks = thumbnails.Select(async thumb =>
            {
                var blobClient = containerClient.GetBlobClient(thumb.FileName);

                using var stream = new MemoryStream(thumb.Data);

                await blobClient.UploadAsync(stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = thumb.Contentype,
                        CacheControl = "public, max-age=31536000"
                    },
                    Metadata = new Dictionary<string, string>
                    {
                       {"SizeSuffix", thumb.SizeSuffix},
                       {"Width",thumb.Width.ToString()},
                       {"Height", thumb.Height.ToString()},
                       {"GenerateAt", DateTime.UtcNow.ToString("o")}
                    }
                });

                _logger.LogInformation($"Upload thumbnail: {thumb.FileName}");
            });

             await Task.WhenAll(uploadTasks);

            _logger.LogInformation($"All {thumbnails.Count} thumbnails uploaded");
        }
    }
}