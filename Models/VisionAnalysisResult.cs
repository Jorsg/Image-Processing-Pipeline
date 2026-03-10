namespace az204_image_processor.Models
{
    public class VisionAnalysisResult
    {
        //OCR - extractet text
        public string ExtractedText { get; set; } = string.Empty;
        public List<TextLine> TextLines { get; set; } = new ();
    }
}