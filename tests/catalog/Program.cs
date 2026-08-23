using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

const string expectedHost = "127.0.0.1";
const string expectedDatabase = "yagot_performance_main";
const string expectedUser = "yagot_performance_app";

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MYDB")
    ?? throw new InvalidOperationException("The approved Performance connection is missing.");
var builder = new NpgsqlConnectionStringBuilder(connectionString);
if (builder.Host != expectedHost || builder.Port != 5432 ||
    builder.Database != expectedDatabase || builder.Username != expectedUser ||
    builder.SslMode != SslMode.Disable || string.IsNullOrWhiteSpace(builder.Passfile))
{
    throw new InvalidOperationException("The database target is outside the approved Performance boundary.");
}

var expectedPassfile = Path.GetFullPath(Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "YAGOT", "PerformanceCredentials", "pgpass.conf"));
if (!string.Equals(Path.GetFullPath(builder.Passfile), expectedPassfile, StringComparison.OrdinalIgnoreCase) ||
    !File.Exists(expectedPassfile) ||
    Environment.GetEnvironmentVariable("PGREQUIREAUTH") != "scram-sha-256")
{
    throw new InvalidOperationException("The approved SCRAM credential boundary is not active.");
}

var options = new DbContextOptionsBuilder<NeondbContext>()
    .UseNpgsql(connectionString)
    .Options;
await using var context = new NeondbContext(options);
var catalog = new ProductCatalogService(context);

var identity = await context.Database.SqlQueryRaw<string>(
    "SELECT (current_user || '|' || current_database() || '|' || host(inet_server_addr()) || '|' || inet_server_port()::text) AS \"Value\"")
    .SingleAsync();
if (identity != $"{expectedUser}|{expectedDatabase}|{expectedHost}|5432")
    throw new InvalidOperationException("The connected database identity is not approved.");

var expectedIds = await context.Products.AsNoTracking()
    .Where(product => product.Stockquantity > 0)
    .OrderByDescending(product => product.Createdat ?? DateTime.MinValue)
    .ThenByDescending(product => product.Id)
    .Select(product => product.Id)
    .ToListAsync();

var actualIds = new List<int>();
ProductsCatalogViewModel page;
var pageNumber = 1;
do
{
    page = await catalog.GetCatalogAsync(new ProductsCatalogRequest { Page = pageNumber });
    actualIds.AddRange(page.Products.Select(product => product.Id));
    pageNumber++;
} while (page.HasNextPage);

Assert(page.TotalCount == expectedIds.Count, "The total result count differs from the database count.");
Assert(actualIds.SequenceEqual(expectedIds), "Catalog pages contain duplicate, missing, or misordered products.");
Assert(actualIds.Distinct().Count() == actualIds.Count, "Catalog pages contain duplicate product IDs.");

foreach (var sort in new[] { "price-asc", "price-desc", "name" })
{
    var result = await catalog.GetCatalogAsync(new ProductsCatalogRequest { Sort = sort });
    var expected = sort switch
    {
        "price-asc" => await context.Products.AsNoTracking().Where(p => p.Stockquantity > 0)
            .OrderBy(p => p.Price).ThenBy(p => p.Id).Select(p => p.Id).Take(ProductCatalogService.PageSize).ToListAsync(),
        "price-desc" => await context.Products.AsNoTracking().Where(p => p.Stockquantity > 0)
            .OrderByDescending(p => p.Price).ThenBy(p => p.Id).Select(p => p.Id).Take(ProductCatalogService.PageSize).ToListAsync(),
        _ => await context.Products.AsNoTracking().Where(p => p.Stockquantity > 0)
            .OrderBy(p => p.Name).ThenBy(p => p.Id).Select(p => p.Id).Take(ProductCatalogService.PageSize).ToListAsync()
    };
    Assert(result.Products.Select(product => product.Id).SequenceEqual(expected), $"Global {sort} sorting differs from the database.");
}

var activeCategories = await context.Products.AsNoTracking()
    .Where(product => product.Stockquantity > 0)
    .GroupBy(product => product.Categoryid)
    .Select(group => new { CategoryId = group.Key, Count = group.Count() })
    .ToListAsync();
foreach (var category in activeCategories)
{
    var result = await catalog.GetCatalogAsync(new ProductsCatalogRequest { CategoryId = category.CategoryId });
    Assert(result.TotalCount == category.Count, $"Category {category.CategoryId} count differs.");
}

var activeBrands = await context.Products.AsNoTracking()
    .Where(product => product.Stockquantity > 0 && product.Brand != null && product.Brand != "-")
    .Select(product => product.Brand!)
    .Distinct()
    .ToListAsync();
foreach (var brand in activeBrands)
{
    var result = await catalog.GetCatalogAsync(new ProductsCatalogRequest { Brand = [brand] });
    var expectedCount = await context.Products.CountAsync(product =>
        product.Stockquantity > 0 && product.Brand != null && EF.Functions.ILike(product.Brand, brand));
    Assert(result.TotalCount == expectedCount, $"Brand filter count differs for a current brand.");
}

if (activeBrands.Count >= 3)
{
    var a = activeBrands[0];
    var b = activeBrands[1];
    var c = activeBrands[2];

    // A + B
    var resultAB = await catalog.GetCatalogAsync(new ProductsCatalogRequest { Brand = [a, b] });
    var expectedCountAB = await context.Products.CountAsync(product =>
        product.Stockquantity > 0 && product.Brand != null &&
        (EF.Functions.ILike(product.Brand, a) || EF.Functions.ILike(product.Brand, b)));
    Assert(resultAB.TotalCount == expectedCountAB, "A+B multi-brand OR filter count differs.");

    // A + B + C
    var resultABC = await catalog.GetCatalogAsync(new ProductsCatalogRequest { Brand = [a, b, c] });
    var expectedCountABC = await context.Products.CountAsync(product =>
        product.Stockquantity > 0 && product.Brand != null &&
        (EF.Functions.ILike(product.Brand, a) || EF.Functions.ILike(product.Brand, b) || EF.Functions.ILike(product.Brand, c)));
    Assert(resultABC.TotalCount == expectedCountABC, "A+B+C multi-brand OR filter count differs.");

    // duplicate brand input
    var resultDup = await catalog.GetCatalogAsync(new ProductsCatalogRequest { Brand = [a, a, b] });
    Assert(resultDup.TotalCount == expectedCountAB, "Duplicate brands are not normalized.");

    // case handling
    var resultCase = await catalog.GetCatalogAsync(new ProductsCatalogRequest { Brand = [a.ToUpper(), b.ToLower()] });
    Assert(resultCase.TotalCount == expectedCountAB, "Brand case is not handled case-insensitively.");

    // unknown brand
    var resultUnknown = await catalog.GetCatalogAsync(new ProductsCatalogRequest { Brand = ["ThisBrandDoesNotExist12345"] });
    Assert(resultUnknown.TotalCount == 0, "Unknown brand returned results.");

    // brand + category
    var sampleCategory = activeCategories.First();
    var resultBrandCat = await catalog.GetCatalogAsync(new ProductsCatalogRequest { CategoryId = sampleCategory.CategoryId, Brand = [a] });
    var expectedBrandCat = await context.Products.CountAsync(product =>
        product.Stockquantity > 0 && product.Categoryid == sampleCategory.CategoryId && product.Brand != null && EF.Functions.ILike(product.Brand, a));
    Assert(resultBrandCat.TotalCount == expectedBrandCat, "Brand + Category filter count differs.");
}

var invalidBrandLengthReq = new ProductsCatalogRequest { Brand = [new string('x', 151)] };
var valResults = new List<ValidationResult>();
Assert(!Validator.TryValidateObject(invalidBrandLengthReq, new ValidationContext(invalidBrandLengthReq), valResults, true), "Overlong brand string was accepted.");

var tooManyBrandsReq = new ProductsCatalogRequest { Brand = Enumerable.Range(1, 25).Select(i => i.ToString()).ToArray() };
var valResultsMax = new List<ValidationResult>();
// In this case, either Validation catches it via MaxLength on array, or ProductCatalogService caps it.
// According to our changes, ProductCatalogService caps it at 20 without validation error, but MaxLength(20) is on the array.
Assert(!Validator.TryValidateObject(tooManyBrandsReq, new ValidationContext(tooManyBrandsReq), valResultsMax, true), "Too many selected brands were accepted.");

var minimumPrice = await context.Products.Where(product => product.Stockquantity > 0).MinAsync(product => (decimal?)product.Price);
var maximumPrice = await context.Products.Where(product => product.Stockquantity > 0).MaxAsync(product => (decimal?)product.Price);
if (minimumPrice.HasValue && maximumPrice.HasValue)
{
    var result = await catalog.GetCatalogAsync(new ProductsCatalogRequest { MinPrice = minimumPrice, MaxPrice = maximumPrice });
    Assert(result.TotalCount == expectedIds.Count, "Inclusive base-price range differs from the database.");
}

var retailYes = await catalog.GetCatalogAsync(new ProductsCatalogRequest { Retail = "yes" });
var retailNo = await catalog.GetCatalogAsync(new ProductsCatalogRequest { Retail = "no" });
Assert(retailYes.TotalCount == await context.Products.CountAsync(p => p.Stockquantity > 0 && p.IsRetailEnabled), "Retail=yes count differs.");
Assert(retailNo.TotalCount == await context.Products.CountAsync(p => p.Stockquantity > 0 && !p.IsRetailEnabled), "Retail=no count differs.");

var contradictoryRetail = new ProductsCatalogRequest { Retail = "no", RetailSize = [5] };
var normalizedRetailNo = await catalog.GetCatalogAsync(contradictoryRetail);
Assert(contradictoryRetail.RetailSize.Length == 0, "Retail=no did not clear the contradictory retail-size request state.");
Assert(normalizedRetailNo.TotalCount == retailNo.TotalCount, "Retail=no plus retail size changed retail=no catalog semantics.");

var expectedRetailSizes = await context.ProductRetailPrices.AsNoTracking()
    .Where(price =>
        price.IsActive &&
        price.SizeMl > 0 &&
        price.Product.IsRetailEnabled &&
        price.Product.Stockquantity > 0)
    .Select(price => price.SizeMl)
    .Distinct()
    .OrderBy(size => size)
    .ToListAsync();
Assert(retailYes.RetailSizes.SequenceEqual(expectedRetailSizes), "Retail-size filter options differ from active database rows.");

var retailSizeCounts = new Dictionary<int, int>();
foreach (var size in expectedRetailSizes)
{
    var result = await catalog.GetCatalogAsync(new ProductsCatalogRequest { RetailSize = [size] });
    var expectedCount = await context.Products.CountAsync(product =>
        product.Stockquantity > 0 && product.IsRetailEnabled &&
        product.RetailPrices.Any(price => price.IsActive && price.SizeMl == size));
    Assert(result.TotalCount == expectedCount, $"Retail size {size} count differs.");
    retailSizeCounts[size] = result.TotalCount;
}

var sample = await context.Products.AsNoTracking()
    .Where(product => product.Stockquantity > 0)
    .OrderBy(product => product.Id)
    .FirstOrDefaultAsync();
if (sample is not null)
{
    var search = await catalog.GetCatalogAsync(new ProductsCatalogRequest { Search = sample.Name });
    Assert(search.Products.Any(product => product.Id == sample.Id), "A real product-name search omitted its source product.");

    var combined = await catalog.GetCatalogAsync(new ProductsCatalogRequest
    {
        Search = sample.Name,
        CategoryId = sample.Categoryid,
        Brand = sample.Brand != null ? [sample.Brand] : [],
        MinPrice = sample.Price,
        MaxPrice = sample.Price,
        Sort = "price-asc"
    });
    Assert(combined.Products.Any(product => product.Id == sample.Id), "A real matching product is missing from combined filters.");
}

var invalid = new ProductsCatalogRequest
{
    MinPrice = 10,
    MaxPrice = 1,
    Retail = "invalid",
    RetailSize = [0],
    Sort = "invalid"
};
var validationResults = new List<ValidationResult>();
Assert(!Validator.TryValidateObject(invalid, new ValidationContext(invalid), validationResults, true), "Invalid filters were accepted.");

Console.WriteLine("CATALOG RUNTIME CHECKS PASSED");
Console.WriteLine($"Database={expectedDatabase}; ActiveProducts={expectedIds.Count}; Pages={Math.Max(1, pageNumber - 1)}");
Console.WriteLine($"RetailYes={retailYes.TotalCount}; RetailNo={retailNo.TotalCount}; InvalidCases={validationResults.Count}");
Console.WriteLine("RetailSizes=" + string.Join(",", retailSizeCounts.Select(pair => $"{pair.Key}:{pair.Value}")));

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
