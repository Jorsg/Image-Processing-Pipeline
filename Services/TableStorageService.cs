using System.Text.Json;
using System.Text.Json.Nodes;
using az204_image_processor.Models;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;

namespace az204_image_processor.Services
{
    public class TableStorageService : ITableStorageService
    {
        private readonly TableClient _tableClient;
        private readonly ILogger<TableStorageService> _logger;


        public TableStorageService(ILogger<TableStorageService> logger, TableClient tableClient)
        {
            _logger = logger;

            var connectionString = Environment.GetEnvironmentVariable("ImageStorageConnection");

            _tableClient = new TableClient(connectionString, "ImageMetadata");

            _tableClient.CreateIfNotExistsAsync();
        }
        public async Task<ImageMetadataEntity?> GetImageMetadataEntityAsync(string data, string fileName)
        {
            var result = new ImageMetadataEntity();
            try
            {
               result =  await _tableClient.GetEntityAsync<ImageMetadataEntity>(data, fileName);
                return result;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {                
                _logger.LogWarning($"ImagaMetadaEntity not fond {ex.Message}");
                return null;
            }           
        }

        public async Task SaveImageMetadataAsync(string fileName, ImageInfo imageInfo, VisionAnalysisResult visionResult, double processingTimeMs)
        {

            var imageMetadata = new ImageMetadataEntity();
            imageMetadata.PartitionKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
            imageMetadata.RowKey = fileName;
            imageMetadata.OriginalHeight = imageInfo.OriginalHeight;
            imageMetadata.OriginalWidth = imageInfo.OriginalWidth;
            imageMetadata.FileSizeBytes = imageInfo.FileSizeBytes;
            imageMetadata.Format = imageInfo.Format;
            imageMetadata.Description = visionResult.Description;
            imageMetadata.DescriptionConfidence = visionResult.DescriptionConfidence;
            imageMetadata.ExtractedText = visionResult.ExtractedText;
            imageMetadata.Tags = JsonSerializer.Serialize(visionResult.Tags);
            imageMetadata.ProcessingTimeMs = processingTimeMs;

            await _tableClient.UpsertEntityAsync(imageMetadata);
        }
    }
}