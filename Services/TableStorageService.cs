using az204_image_processor.Models;
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
        public Task<ImageMetadataEntity?> GetImageMetadataEntityAsync(string data, string fileName)
        {

            throw new NotImplementedException();
        }

        public Task SaveImageMetadataAsync(string fileName, ImageInfo imageInfo, VisionAnalysisResult visionResult, double processingTimeMs)
        {

            var imageMetadata = new ImageMetadataEntity();
            imageMetadata.PartitionKey = DateTime.Now.ToString("yyyy-MM-dd");
            imageMetadata.RowKey = fileName;
            var imageInf = new ImageInfo
            {
                OriginalHeight = imageInfo.OriginalHeight,
                OriginalWidth = imageInfo.OriginalWidth,
                FileSizeBytes = imageInfo.FileSizeBytes,
                Format = imageInfo.Format
            };

            var vsion_Result = new VisionAnalysisResult
            {
              ApiCalDurationMs = visionResult.ApiCalDurationMs,
              Description = visionResult.Description,
              Categories = visionResult.Categories,
              DescriptionConfidence = visionResult.DescriptionConfidence  
            };



            throw new NotImplementedException();
        }
    }
}