using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using System.Text.Json;
using YAGOT_2._0.Controllers;
using YAGOT_2._0.Services;
using ImageStorage = YAGOT_2._0.Services.Image;

if (args.Length == 2 && args[0] == "--audit-database")
{
    await AuditDatabaseReferences(args[1]);
    return;
}

var testRoot = Path.Combine(Path.GetTempPath(), $"yagot-image-storage-{Guid.NewGuid():N}");
var webRoot = Path.Combine(testRoot, "wwwroot");
Directory.CreateDirectory(webRoot);

try
{
    var environment = new TestEnvironment(testRoot, webRoot);
    var storage = new ImageStorage(environment);

    var productName = await storage.UploadImage(FormFile("product.png"), "products")
        ?? throw new Exception("Product upload returned null.");
    AssertFile(Path.Combine(testRoot, "App_Data", "images", "products", productName));
    Assert(!File.Exists(Path.Combine(webRoot, "images", "products", productName)), "Product was written to wwwroot.");

    var categoryName = await storage.UploadImage(FormFile("category.webp"), "categories")
        ?? throw new Exception("Category upload returned null.");
    AssertFile(Path.Combine(testRoot, "App_Data", "images", "categories", categoryName));
    Assert(!File.Exists(Path.Combine(webRoot, "images", "categories", categoryName)), "Category was written to wwwroot.");

    var replacementUrl = await storage.UpdateImage(FormFile("replacement.jpg"), "products", $"/images/products/{productName}")
        ?? throw new Exception("Product replacement returned null.");
    Assert(File.Exists(Path.Combine(testRoot, "App_Data", "images", "products", Path.GetFileName(replacementUrl))), "Replacement was not stored.");
    Assert(File.Exists(Path.Combine(testRoot, "App_Data", "images", "products", productName)), "Storage service deleted the old image before database persistence.");
    Assert(storage.DeleteImage("products", productName), "Old product image was not deleted on request.");

    Assert(storage.TryOpenImage("categories", categoryName, out var imageStream, out var imageType), "Stored category image could not be opened.");
    imageStream!.Dispose();
    Assert(imageType == "image/webp", "Incorrect category MIME type.");

    var categoryReplacementUrl = await storage.UpdateImage(FormFile("category-replacement.png"), "categories", $"images/categories/{categoryName}")
        ?? throw new Exception("Category replacement returned null.");
    Assert(File.Exists(Path.Combine(testRoot, "App_Data", "images", "categories", Path.GetFileName(categoryReplacementUrl))), "Category replacement was not stored.");
    Assert(File.Exists(Path.Combine(testRoot, "App_Data", "images", "categories", categoryName)), "Storage service deleted the old category image before database persistence.");

    var controller = new ImagesController(storage);
    var served = controller.Get("products", Path.GetFileName(replacementUrl));
    Assert(served is FileStreamResult, "Image endpoint did not return a file.");
    ((FileStreamResult)served).FileStream.Dispose();
    Assert(controller.Get("receipts", "secret.png") is NotFoundResult, "Disallowed directory was accessible.");

    foreach (var unsafeName in new[] { "../secret.png", "..\\secret.png", "%2e%2e%2fsecret.png", "..", "secret.txt", "/secret.png" })
    {
        Assert(!storage.TryOpenImage("products", unsafeName, out _, out _), $"Unsafe filename was accepted: {unsafeName}");
    }

    var receiptStorage = new ReceiptStorageService(environment);
    var stagedReceipt = await receiptStorage.StageAsync(FormFile("receipt.png"));
    receiptStorage.Promote(stagedReceipt);
    Assert(File.Exists(Path.Combine(testRoot, "App_Data", "ProtectedReceipts", stagedReceipt.StorageKey)), "Receipt did not remain in ProtectedReceipts.");
    Assert(receiptStorage.TryOpen(stagedReceipt.StorageKey, out var receiptStream, out var receiptType), "Protected receipt could not be opened.");
    receiptStream!.Dispose();
    Assert(receiptType == "image/png", "Incorrect receipt MIME type.");

    Assert(storage.DeleteImage("categories", $"images/categories/{categoryName}"), "Category replacement cleanup did not accept the existing relative database form.");
    Assert(storage.DeleteImage("categories", categoryReplacementUrl), "Category deletion failed.");
    Assert(storage.DeleteImage("products", replacementUrl), "Product replacement cleanup failed.");
    Console.WriteLine("Image storage verification passed.");
}
finally
{
    if (Directory.Exists(testRoot))
        Directory.Delete(testRoot, recursive: true);
}

static FormFile FormFile(string name)
{
    byte[] bytes = Path.GetExtension(name).Equals(".png", StringComparison.OrdinalIgnoreCase)
        ? [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]
        : [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
    var stream = new MemoryStream(bytes);
    return new FormFile(stream, 0, bytes.Length, "file", name);
}

static void AssertFile(string path) => Assert(File.Exists(path), $"Expected file does not exist: {path}");
static void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

static async Task AuditDatabaseReferences(string settingsPath)
{
    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
    var connectionString = document.RootElement
        .GetProperty("ConnectionStrings")
        .GetProperty("MYDB")
        .GetString();
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new Exception("MYDB connection string is missing.");

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    const string sql = """
        select 'products' as image_type, imageurl from products where imageurl is not null
        union all
        select 'categories' as image_type, imageurl from categories where imageurl is not null
        """;
    await using var command = new NpgsqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var nonCanonical = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var nonCanonicalKinds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var missingLocalFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    var projectRoot = Path.GetDirectoryName(Path.GetFullPath(settingsPath))!;
    while (await reader.ReadAsync())
    {
        var imageType = reader.GetString(0);
        var value = reader.GetString(1).Replace('\\', '/');
        totals[imageType] = totals.GetValueOrDefault(imageType) + 1;
        var expectedPrefix = $"/images/{imageType}/";
        var relativePrefix = $"images/{imageType}/";
        if (value.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(relativePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var fileName = value[(value.LastIndexOf('/') + 1)..];
            var physicalPath = Path.Combine(projectRoot, "App_Data", "images", imageType, fileName);
            if (!File.Exists(physicalPath))
            {
                if (!missingLocalFiles.TryGetValue(imageType, out var missing))
                {
                    missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    missingLocalFiles[imageType] = missing;
                }
                missing.Add(fileName);
            }
        }
        if (!value.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            nonCanonical[imageType] = nonCanonical.GetValueOrDefault(imageType) + 1;
            var kind = value.StartsWith($"images/{imageType}/", StringComparison.OrdinalIgnoreCase)
                ? "relative-image-url"
                : value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? "external-url"
                    : value.Contains("wwwroot", StringComparison.OrdinalIgnoreCase)
                        ? "wwwroot-path"
                        : Path.IsPathRooted(value)
                            ? "absolute-path"
                            : "other";
            var key = $"{imageType}:{kind}";
            nonCanonicalKinds[key] = nonCanonicalKinds.GetValueOrDefault(key) + 1;
        }
    }

    foreach (var imageType in new[] { "products", "categories" })
    {
        Console.WriteLine($"{imageType}: total={totals.GetValueOrDefault(imageType)}, nonCanonical={nonCanonical.GetValueOrDefault(imageType)}, missingLocalFiles={missingLocalFiles.GetValueOrDefault(imageType)?.Count ?? 0}");
        if (missingLocalFiles.TryGetValue(imageType, out var missing))
        {
            foreach (var fileName in missing.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine($"  missing={fileName}");
        }
        foreach (var item in nonCanonicalKinds.Where(item => item.Key.StartsWith(imageType + ":", StringComparison.OrdinalIgnoreCase)))
            Console.WriteLine($"  {item.Key[(item.Key.IndexOf(':') + 1)..]}={item.Value}");
    }
}

sealed class TestEnvironment : IWebHostEnvironment
{
    public TestEnvironment(string contentRootPath, string webRootPath)
    {
        ContentRootPath = contentRootPath;
        WebRootPath = webRootPath;
    }

    public string ApplicationName { get; set; } = "ImageStorageVerification";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; }
    public string EnvironmentName { get; set; } = "Testing";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; }
}
