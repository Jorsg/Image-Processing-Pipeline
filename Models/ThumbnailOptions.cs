using az204_image_processor.Models;
namespace az204_image_processor.Models
{
    public class ThumbnailOptions
    {
        public ThumbnailSize[] Sizes { get; set; } =
        {
            new() { Suffix ="sm", Width = 100, Height = 100, Quality = 70},
            new() { Suffix ="md", Width = 300, Height = 300, Quality = 70},
            new() { Suffix ="lg", Width = 600, Height = 600, Quality = 80},
        };

        public bool AddWaterMark {get; set;} = false;
        public string WatermarkText { get; set; } = "@ AZ-204 Demo";
        public bool PreserveTransparency { get; set; } = true;
    }
}