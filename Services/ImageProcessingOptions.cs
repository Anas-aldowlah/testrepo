namespace YAGOT_2._0.Services;

public sealed class ImageProcessingOptions
{
    public const string SectionName = "ImageProcessing";

    /// <summary>
    /// Initial WebP quality (0-100). Default is 82 for high visual fidelity with significant size reduction.
    /// </summary>
    public int WebpQuality { get; set; } = 82;

    /// <summary>
    /// Maximum allowed upload size in bytes (default: 5 MB).
    /// </summary>
    public long MaxUploadBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Maximum bounding box limit for product images.
    /// Default: 1200 x 1200 px. Proportional fit without cropping, padding, or stretching.
    /// </summary>
    public ImageDimensionLimit ProductDimensions { get; set; } = new(1200, 1200);

    /// <summary>
    /// Maximum bounding box limit for category images.
    /// Default: 600 x 600 px. Proportional fit without cropping, padding, or stretching.
    /// </summary>
    public ImageDimensionLimit CategoryDimensions { get; set; } = new(600, 600);

    /// <summary>
    /// Maximum bounding box limit for future Hero/banner uploads.
    /// Default: 1920 x 1080 px. Proportional fit without cropping, padding, or stretching.
    /// </summary>
    public ImageDimensionLimit HeroDimensions { get; set; } = new(1920, 1080);
}

public sealed record ImageDimensionLimit(int MaxWidth, int MaxHeight);
