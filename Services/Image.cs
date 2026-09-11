using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace YAGOT_2._0.Services;

public sealed class Image
{
    private static readonly HashSet<string> UploadExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private static readonly HashSet<string> ServedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".jfif", ".png", ".webp" };

    private static readonly HashSet<string> AllowedFolders =
        new(StringComparer.OrdinalIgnoreCase) { "products", "categories", "hero", "banners" };

    private const long MaxFileSize = 5 * 1024 * 1024;
    private readonly string _imagesRoot;
    private readonly ImageProcessingOptions _options;
    private readonly ILogger<Image>? _logger;

    public Image(
        IWebHostEnvironment environment,
        IOptions<ImageProcessingOptions>? options = null,
        ILogger<Image>? logger = null)
    {
        _imagesRoot = Path.Combine(environment.ContentRootPath, "App_Data", "images");
        _options = options?.Value ?? new ImageProcessingOptions();
        _logger = logger;
    }

    public async Task<string?> UploadImage(IFormFile? imageFile, string subFolder)
    {
        if (imageFile == null || imageFile.Length == 0 || GetValidationError(imageFile) != null)
            return null;

        if (imageFile.Length > _options.MaxUploadBytes)
        {
            _logger?.LogWarning("Upload rejected: image size {Size} bytes exceeds maximum {Max} bytes.", imageFile.Length, _options.MaxUploadBytes);
            return null;
        }

        if (!AllowedFolders.Contains(subFolder))
        {
            _logger?.LogWarning("Upload rejected: unsupported subFolder '{SubFolder}'.", subFolder);
            return null;
        }

        var directory = GetImageDirectory(subFolder);
        Directory.CreateDirectory(directory);

        try
        {
            // 1. Read input stream into memory safely
            await using var inputStream = imageFile.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await inputStream.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();

            // 2. Validate actual binary image signature using SkiaSharp codec
            using var skData = SKData.CreateCopy(imageBytes);
            using var codec = SKCodec.Create(skData);
            if (codec == null)
            {
                _logger?.LogWarning("Upload rejected: file content does not represent a valid supported image.");
                return null;
            }

            var format = codec.EncodedFormat;
            if (format is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            {
                _logger?.LogWarning("Upload rejected: encoded image format {Format} is not allowed.", format);
                return null;
            }

            // 3. Decode bitmap safely
            using var originalBitmap = SKBitmap.Decode(skData);
            if (originalBitmap == null || originalBitmap.Width <= 0 || originalBitmap.Height <= 0)
            {
                _logger?.LogWarning("Upload rejected: decoded bitmap is empty or corrupted.");
                return null;
            }

            // 4. Resolve dimension limits
            var limit = ResolveDimensionLimit(subFolder);

            // 5. Proportional dimension calculation:
            // - Preserves aspect ratio
            // - No crop, no stretch, no padding
            // - No upscale (if smaller than max, preserves original size)
            int targetWidth = originalBitmap.Width;
            int targetHeight = originalBitmap.Height;

            if (originalBitmap.Width > limit.MaxWidth || originalBitmap.Height > limit.MaxHeight)
            {
                float ratioX = (float)limit.MaxWidth / originalBitmap.Width;
                float ratioY = (float)limit.MaxHeight / originalBitmap.Height;
                float ratio = Math.Min(ratioX, ratioY);

                targetWidth = Math.Max(1, (int)Math.Round(originalBitmap.Width * ratio));
                targetHeight = Math.Max(1, (int)Math.Round(originalBitmap.Height * ratio));
            }

            SKBitmap toEncode = originalBitmap;
            SKBitmap? resizedBitmap = null;

            try
            {
                if (targetWidth != originalBitmap.Width || targetHeight != originalBitmap.Height)
                {
                    // Preserve color type and alpha transparency channel
                    var resizeInfo = new SKImageInfo(targetWidth, targetHeight, originalBitmap.ColorType, originalBitmap.AlphaType);
                    resizedBitmap = originalBitmap.Resize(resizeInfo, SKFilterQuality.High);
                    if (resizedBitmap != null)
                    {
                        toEncode = resizedBitmap;
                    }
                }

                // 6. Encode to WebP preserving transparency and high visual fidelity
                using var skImage = SKImage.FromBitmap(toEncode);
                using var encodedData = skImage.Encode(SKEncodedImageFormat.Webp, _options.WebpQuality);
                if (encodedData == null || encodedData.IsEmpty)
                {
                    _logger?.LogError("Failed to encode image to WebP format.");
                    return null;
                }

                // 7. Save with safe unique GUID name and .webp extension
                var uniqueFileName = $"{Guid.NewGuid():N}.webp";
                var filePath = Path.Combine(directory, uniqueFileName);

                await using (var fileStream = new FileStream(
                    filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    encodedData.SaveTo(fileStream);
                }

                _logger?.LogInformation(
                    "Image optimized: {FileName} ({OriginalW}x{OriginalH} -> {TargetW}x{TargetH}, {OriginalBytes}B -> {OptimizedBytes}B)",
                    uniqueFileName, originalBitmap.Width, originalBitmap.Height, targetWidth, targetHeight, imageFile.Length, encodedData.Size);

                return uniqueFileName;
            }
            finally
            {
                resizedBitmap?.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error occurred while optimizing uploaded image for {SubFolder}.", subFolder);
            return null;
        }
    }

    private ImageDimensionLimit ResolveDimensionLimit(string subFolder)
    {
        return subFolder.ToLowerInvariant() switch
        {
            "categories" => _options.CategoryDimensions,
            "hero" or "banners" => _options.HeroDimensions,
            _ => _options.ProductDimensions
        };
    }

    public static string? GetValidationError(IFormFile? imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
            return null;

        if (imageFile.Length > MaxFileSize)
            return "يجب ألا يتجاوز حجم الصورة 5 ميجابايت.";

        var extension = Path.GetExtension(imageFile.FileName);
        return UploadExtensions.Contains(extension)
            ? null
            : "صيغة الصورة غير مدعومة. استخدم صورة بصيغة مدعومة.";
    }

    public async Task<string?> UpdateImage(
        IFormFile? imageFile,
        string subFolder,
        string existingImage)
    {
        var uniqueFileName = await UploadImage(imageFile, subFolder);
        return uniqueFileName == null ? null : $"/images/{subFolder}/{uniqueFileName}";
    }

    public bool DeleteImage(string subFolder, string? storedReference)
    {
        var fileName = ExtractSafeFileName(storedReference);
        if (fileName == null)
            return false;

        var filePath = Path.Combine(GetImageDirectory(subFolder), fileName);
        if (!File.Exists(filePath))
            return false;

        File.Delete(filePath);
        return true;
    }

    public bool TryOpenImage(
        string subFolder,
        string fileName,
        out FileStream? stream,
        out string contentType)
    {
        return TryOpenImage(subFolder, fileName, out stream, out contentType, out _, out _);
    }

    public bool TryOpenImage(
        string subFolder,
        string fileName,
        out FileStream? stream,
        out string contentType,
        out DateTimeOffset lastModifiedUtc,
        out long fileLength)
    {
        stream = null;
        contentType = "application/octet-stream";
        lastModifiedUtc = default;
        fileLength = 0;

        if (!AllowedFolders.Contains(subFolder) || !IsSafeFileName(fileName))
            return false;

        contentType = GetContentType(fileName);
        if (contentType == "application/octet-stream")
            return false;

        var filePath = Path.Combine(GetImageDirectory(subFolder), fileName);
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            return false;

        lastModifiedUtc = new DateTimeOffset(fileInfo.LastWriteTimeUtc);
        fileLength = fileInfo.Length;

        stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return true;
    }

    private string GetImageDirectory(string subFolder)
    {
        if (!AllowedFolders.Contains(subFolder))
            throw new ArgumentException("Unsupported image folder.", nameof(subFolder));

        return Path.Combine(_imagesRoot, subFolder.ToLowerInvariant());
    }

    private static string? ExtractSafeFileName(string? storedReference)
    {
        if (string.IsNullOrWhiteSpace(storedReference))
            return null;

        var normalized = storedReference.Replace('\\', '/');
        var fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
        return IsSafeFileName(fileName) ? fileName : null;
    }

    private static bool IsSafeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
            return false;

        if (fileName.Contains('/') || fileName.Contains('\\') ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        return ServedExtensions.Contains(Path.GetExtension(fileName));
    }

    private static string GetContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".jfif" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
}
