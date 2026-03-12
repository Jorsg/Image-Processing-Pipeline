using Azure;
using Azure.Data.Tables;

namespace az204_image_processor.Models
{ 
    public class ImageMetadataEntity : ITableEntity
    {
        // PartitionKey = processing date (your choice!)
        // RowKey = file name
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

        // Image info (from Step 3)
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }
        public string Format { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }

        // Vision results (from Step 5)
        public string Description { get; set; } = string.Empty;
        public double DescriptionConfidence { get; set; }
        public string Tags { get; set; } = string.Empty;       // JSON string
        public string ExtractedText { get; set; } = string.Empty;

        // Processing info
        public string Status { get; set; } = "Completed";
        public double ProcessingTimeMs { get; set; }
    }
    
}