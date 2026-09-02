using Microsoft.AspNetCore.Html;

namespace YAGOT_2._0.Models
{
    public class PageHeaderViewModel
    {
        public string Variant { get; set; } = "Utility";
        public string Title { get; set; } = string.Empty;
        public string? Eyebrow { get; set; }
        public string? EyebrowIcon { get; set; }
        public string? Subtitle { get; set; }
        public IHtmlContent? Breadcrumbs { get; set; }
        public IHtmlContent? Actions { get; set; }
        public IHtmlContent? Metadata { get; set; }
    }
}
