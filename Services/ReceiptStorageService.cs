namespace YAGOT_2._0.Services;

public sealed class ReceiptStorageService
{
    public const long MaximumFileSize = 5 * 1024 * 1024;

    private readonly string _temporaryDirectory;
    private readonly string _protectedDirectory;

    public ReceiptStorageService(IWebHostEnvironment environment)
    {
        var appDataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        _temporaryDirectory = Path.Combine(appDataDirectory, "TemporaryReceipts");
        _protectedDirectory = Path.Combine(appDataDirectory, "ProtectedReceipts");
    }

    public async Task<StagedReceipt> StageAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length <= 0)
            throw new InvalidDataException("No receipt image was uploaded.");
        if (file.Length > MaximumFileSize)
            throw new InvalidDataException("The receipt image must not exceed 5 MB.");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var headerLength = await ReadHeaderAsync(input, header, cancellationToken);
        if (headerLength < header.Length)
            throw new InvalidDataException("The uploaded receipt is not a complete supported image.");

        var imageType = DetectImageType(header.AsSpan(0, headerLength))
            ?? throw new InvalidDataException("The uploaded receipt is not a supported JPEG, PNG, or WebP image.");

        Directory.CreateDirectory(_temporaryDirectory);
        var storageKey = $"receipt_{Guid.NewGuid():N}{imageType.Extension}";
        var temporaryPath = Path.Combine(_temporaryDirectory, $"{storageKey}.tmp");

        try
        {
            await using var output = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var buffer = new byte[81_920];
            await output.WriteAsync(header.AsMemory(0, headerLength), cancellationToken);
            long bytesWritten = headerLength;
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                bytesWritten += bytesRead;
                if (bytesWritten > MaximumFileSize)
                    throw new InvalidDataException("The receipt image must not exceed 5 MB.");
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            await output.FlushAsync(cancellationToken);
            return new StagedReceipt(storageKey, temporaryPath, imageType.ContentType);
        }
        catch
        {
            DeleteTemporaryFile(temporaryPath);
            throw;
        }
    }

    public void Promote(StagedReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        Directory.CreateDirectory(_protectedDirectory);
        File.Move(receipt.TemporaryPath, GetProtectedPath(receipt.StorageKey), overwrite: false);
    }

    public bool TryOpen(string storedValue, out FileStream? stream, out string contentType)
    {
        stream = null;
        contentType = "application/octet-stream";
        var storageKey = ExtractStorageKey(storedValue);
        if (storageKey == null)
            return false;

        var protectedPath = GetProtectedPath(storageKey);
        if (!File.Exists(protectedPath))
            return false;

        contentType = GetContentType(storageKey);
        stream = new FileStream(
            protectedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return true;
    }

    public static void DeleteTemporaryFile(StagedReceipt? receipt)
    {
        if (receipt != null)
            DeleteTemporaryFile(receipt.TemporaryPath);
    }

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private string GetProtectedPath(string storageKey) => Path.Combine(_protectedDirectory, storageKey);

    private static string? ExtractStorageKey(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
            return null;

        var storageKey = Path.GetFileName(storedValue.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return null;

        return GetContentType(storageKey) == "application/octet-stream" ? null : storageKey;
    }

    private static string GetContentType(string storageKey) =>
        Path.GetExtension(storageKey).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

    private static async Task<int> ReadHeaderAsync(Stream stream, byte[] header, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < header.Length)
        {
            var bytesRead = await stream.ReadAsync(header.AsMemory(totalRead), cancellationToken);
            if (bytesRead == 0)
                break;
            totalRead += bytesRead;
        }
        return totalRead;
    }

    private static ImageType? DetectImageType(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return new ImageType(".jpg", "image/jpeg");

        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (header.Length >= pngSignature.Length && header[..pngSignature.Length].SequenceEqual(pngSignature))
            return new ImageType(".png", "image/png");

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
            return new ImageType(".webp", "image/webp");

        return null;
    }

    private sealed record ImageType(string Extension, string ContentType);
}

public sealed record StagedReceipt(string StorageKey, string TemporaryPath, string ContentType);
