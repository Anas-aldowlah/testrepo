using Microsoft.AspNetCore.Mvc;
using ImageStorage = YAGOT_2._0.Services.Image;

namespace YAGOT_2._0.Controllers;

[ApiController]
public sealed class ImagesController : ControllerBase
{
    private readonly ImageStorage _imageStorage;

    public ImagesController(ImageStorage imageStorage)
    {
        _imageStorage = imageStorage;
    }

    [HttpGet("/images/{imageType}/{fileName}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    public IActionResult Get(string imageType, string fileName)
    {
        if (!_imageStorage.TryOpenImage(imageType, fileName, out var stream, out var contentType) ||
            stream == null)
        {
            return NotFound();
        }

        return File(stream, contentType, enableRangeProcessing: true);
    }
}
