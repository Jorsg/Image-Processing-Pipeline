using az204_image_processor.Models;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;

namespace az204_image_processor.Services
{
    public interface ITableStorageService
    {
        Task SaveImageMetadataAsync(string fileName, ImageInfo imageInfo, VisionAnalysisResult visionResult, double processingTimeMs);
        Task<ImageMetadataEntity?> GetImageMetadataEntityAsync(string data, string fileName);
    }
}