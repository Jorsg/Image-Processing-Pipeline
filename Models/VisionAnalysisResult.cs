namespace az204_image_processor.Models
{
    public class VisionAnalysisResult
    {
        //OCR - extractet text
        public string ExtractedText { get; set; } = string.Empty;
        public List<TextLine> TextLines { get; set; } = new();

        //Image description
        public string Description { get; set; } = string.Empty;
        public double DescriptionConfidence { get; set; }

        //Tags
        public List<ImageTag> Tags { get; set; } = new();

        //Category
        public List<ImageCategory> Categories { get; set; } = new();

        //Metadata
        public bool IsAdultContent { get; set; }
        public bool IsRacyContent { get; set; }
        public string DomainantColorForeground { get; set; } = string.Empty;
        public string DomainantColorBackground { get; set; } = string.Empty;

        //Procession Info
        public int RetryCount { get; set; }
        public double ApiCalDurationMs { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}