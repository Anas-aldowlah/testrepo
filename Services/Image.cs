namespace YAGOT_2._0.Services;

public sealed class Image
{
    private static readonly HashSet<string> UploadExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private static readonly HashSet<string> ServedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".jfif", ".png", ".webp" };

    private static readonly HashSet<string> AllowedFolders =
        new(StringComparer.OrdinalIgnoreCase) { "products", "categories" };

    private const long MaxFileSize = 5 * 1024 * 1024;
    private readonly string _imagesRoot;

    public Image(IWebHostEnvironment environment)
    {
        _imagesRoot = Path.Combine(environment.ContentRootPath, "App_Data", "images");
    }

    public async Task<string?> UploadImage(IFormFile? imageFile, string subFolder)
    {
        if (imageFile == null || imageFile.Length == 0 || GetValidationError(imageFile) != null)
            return null;

        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        var directory = GetImageDirectory(subFolder);
        Directory.CreateDirectory(directory);

        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(directory, uniqueFileName);
        await using var fileStream = new FileStream(
            filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await imageFile.CopyToAsync(fileStream);
        return uniqueFileName;
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
        stream = null;
        contentType = "application/octet-stream";

        if (!AllowedFolders.Contains(subFolder) || !IsSafeFileName(fileName))
            return false;

        contentType = GetContentType(fileName);
        if (contentType == "application/octet-stream")
            return false;

        var filePath = Path.Combine(GetImageDirectory(subFolder), fileName);
        if (!File.Exists(filePath))
            return false;

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
