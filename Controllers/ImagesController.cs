using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using YAGOT_2._0.Core.Capabilities;
using ImageStorage = YAGOT_2._0.Services.Image;

namespace YAGOT_2._0.Controllers;

[ApiController]
public sealed class ImagesController : ControllerBase
{
    private readonly ImageStorage _imageStorage;
    private readonly ICapabilityEvaluator _capabilityEvaluator;

    public ImagesController(ImageStorage imageStorage, ICapabilityEvaluator capabilityEvaluator)
    {
        _imageStorage = imageStorage;
        _capabilityEvaluator = capabilityEvaluator;
    }

    [HttpGet("/images/{imageType}/{fileName}")]
    public IActionResult Get(string imageType, string fileName)
    {
        if (string.Equals(imageType, "categories", StringComparison.OrdinalIgnoreCase) &&
            !_capabilityEvaluator.IsFeatureEnabled(CapabilityFeatureCodes.CategoryImages))
        {
            return NotFound();
        }

        if (!_imageStorage.TryOpenImage(
                imageType,
                fileName,
                out var stream,
                out var contentType,
                out var lastModifiedUtc,
                out var fileLength) ||
            stream == null)
        {
            return NotFound();
        }

        if (HttpContext != null)
        {
            Response.Headers.CacheControl = "public, max-age=2592000, stale-while-revalidate=86400";
        }

        var entityTag = new EntityTagHeaderValue($"\"{lastModifiedUtc.Ticks:x}-{fileLength:x}\"");
        return File(stream, contentType, lastModifiedUtc, entityTag, enableRangeProcessing: true);
    }
}
