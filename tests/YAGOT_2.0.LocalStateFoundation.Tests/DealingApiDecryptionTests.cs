using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class DealingApiDecryptionTests
{
    private const string TestKey = "0123456789abcdef0123456789abcdef";
    private const string TestIv = "abcdef0123456789";

    [Fact]
    public void KnownSyntheticCiphertext_DecryptsLocally()
    {
        const string plaintext = "+967 777 123 456";
        var ciphertext = Encrypt(plaintext, TestKey, TestIv);

        Assert.Equal(plaintext, CreateService(TestKey, TestIv).DecryptPhone(ciphertext));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrBlankInput_ReturnsEmptyString(string? input)
    {
        Assert.Equal(string.Empty, CreateService(TestKey, TestIv).DecryptPhone(input));
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("AQID")]
    public void MalformedOrInvalidCiphertext_ReturnsEmptyString(string input)
    {
        Assert.Equal(string.Empty, CreateService(TestKey, TestIv).DecryptPhone(input));
    }

    [Theory]
    [InlineData("short", TestIv)]
    [InlineData(TestKey, "short")]
    public void InvalidKeyOrIv_ReturnsEmptyString(string key, string iv)
    {
        var ciphertext = Encrypt("synthetic", TestKey, TestIv);

        Assert.Equal(string.Empty, CreateService(key, iv).DecryptPhone(ciphertext));
    }

    [Fact]
    public void ServiceResolvesWithoutHttpClientOrExternalApiConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration(TestKey, TestIv));
        services.AddTransient<DealingAPI>();
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<HttpClient>());
        Assert.NotSame(
            provider.GetRequiredService<DealingAPI>(),
            provider.GetRequiredService<DealingAPI>());
    }

    [Fact]
    public void PublicSurfaceAndFields_HaveNoHttpDependency()
    {
        var methods = typeof(DealingAPI).GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();
        var constructor = Assert.Single(typeof(DealingAPI).GetConstructors());
        var parameter = Assert.Single(constructor.GetParameters());

        Assert.Equal([nameof(DealingAPI.DecryptPhone)], methods);
        Assert.Equal(typeof(IConfiguration), parameter.ParameterType);
        Assert.DoesNotContain(
            typeof(DealingAPI).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            field => typeof(HttpClient).IsAssignableFrom(field.FieldType));
    }

    [Fact]
    public void DefaultHttpClientFactory_StillResolvesForVisitService()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddDbContext<NeondbContext>(options => options.UseNpgsql(
            "Host=127.0.0.1;Database=not-used;Username=not-used;Password=not-used"));
        services.AddScoped<IVisitService, VisitService>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<VisitService>(scope.ServiceProvider.GetRequiredService<IVisitService>());
        using var client = scope.ServiceProvider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient();
        Assert.NotNull(client);
    }

    private static DealingAPI CreateService(string key, string iv) =>
        new(Configuration(key, iv));

    private static IConfiguration Configuration(string key, string iv) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = key,
                ["Encryption:IV"] = iv
            })
            .Build();

    private static string Encrypt(string plaintext, string key, string iv)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        using var encryptor = aes.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        return Convert.ToBase64String(
            encryptor.TransformFinalBlock(bytes, 0, bytes.Length));
    }
}
