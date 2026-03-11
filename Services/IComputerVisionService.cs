using az204_image_processor.Models;

namespace az204_image_processor.Services
{
    public interface IComputerVisionService
    {
        Task<VisionAnalysisResult> AnalyzeImageAsync(Stream imageStream);
        Task<VisionAnalysisResult> ExtractTextAsync(Stream imageStream);
        Task<VisionAnalysisResult> FullAnalysisAsync(Stream imageStream);
    }
    
}