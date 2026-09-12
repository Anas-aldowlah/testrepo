using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Core.Capabilities;

namespace YAGOT_2._0.Integration.Capabilities;

public enum CapabilitySnapshotParseFailure { None, MalformedJson, InvalidContract }
public sealed record CapabilitySnapshotParseResult(CapabilitySnapshotV1? Snapshot, CapabilitySnapshotParseFailure Failure, string? Diagnostic = null)
{
    public static CapabilitySnapshotParseResult Success(CapabilitySnapshotV1 value) => new(value, CapabilitySnapshotParseFailure.None);
    public static CapabilitySnapshotParseResult Failed(CapabilitySnapshotParseFailure failure, string? diagnostic = null) => new(null, failure, diagnostic);
}

public interface ICapabilitySnapshotV1JsonParser
{
    CapabilitySnapshotParseResult Parse(ReadOnlyMemory<byte> rawBody);
}

public sealed class CapabilitySnapshotV1JsonParser(ICapabilityCatalog catalog, IOptions<CapabilityIntegrationOptions> options)
    : ICapabilitySnapshotV1JsonParser
{
    private static readonly string[] RootProperties = ["contractVersion", "catalogVersion", "siteId", "revision", "generatedAtUtc", "effectiveAtUtc", "modules", "features"];
    private static readonly string[] ModuleProperties = ["code", "enabled"];
    private static readonly string[] FeatureProperties = ["code", "moduleCode", "enabled"];
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    public CapabilitySnapshotParseResult Parse(ReadOnlyMemory<byte> rawBody)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 16 });
            var root = document.RootElement;
            RequireObject(root, RootProperties);
            RequireKind(root, "contractVersion", JsonValueKind.Number);
            RequireKind(root, "catalogVersion", JsonValueKind.String);
            RequireKind(root, "siteId", JsonValueKind.Number);
            RequireKind(root, "revision", JsonValueKind.Number);
            RequireKind(root, "generatedAtUtc", JsonValueKind.String);
            RequireKind(root, "effectiveAtUtc", JsonValueKind.String);
            RequireKind(root, "modules", JsonValueKind.Array);
            RequireKind(root, "features", JsonValueKind.Array);
            foreach (var value in root.GetProperty("modules").EnumerateArray())
            {
                RequireObject(value, ModuleProperties); RequireKind(value, "code", JsonValueKind.String); RequireKind(value, "enabled", JsonValueKind.True, JsonValueKind.False);
            }
            foreach (var value in root.GetProperty("features").EnumerateArray())
            {
                RequireObject(value, FeatureProperties); RequireKind(value, "code", JsonValueKind.String); RequireKind(value, "moduleCode", JsonValueKind.String); RequireKind(value, "enabled", JsonValueKind.True, JsonValueKind.False);
            }

            var snapshot = JsonSerializer.Deserialize<CapabilitySnapshotV1>(rawBody.Span, SerializerOptions)
                ?? throw new CapabilityContractValidationException("Snapshot is required.");
            snapshot.Validate(catalog, options.Value.SiteId);
            return CapabilitySnapshotParseResult.Success(snapshot);
        }
        catch (JsonException exception)
        {
            return CapabilitySnapshotParseResult.Failed(CapabilitySnapshotParseFailure.MalformedJson, exception.Message);
        }
        catch (Exception exception) when (exception is CapabilityContractValidationException or NotSupportedException or InvalidOperationException)
        {
            return CapabilitySnapshotParseResult.Failed(CapabilitySnapshotParseFailure.InvalidContract, exception.Message);
        }
    }

    private static void RequireObject(JsonElement element, IEnumerable<string> required)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new CapabilityContractValidationException("A JSON object was required.");
        var allowed = required.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!allowed.Contains(property.Name) || !seen.Add(property.Name)) throw new CapabilityContractValidationException($"Unknown or duplicate property '{property.Name}'.");
        if (!seen.SetEquals(allowed)) throw new CapabilityContractValidationException("A required property is missing.");
    }

    private static void RequireKind(JsonElement element, string property, params JsonValueKind[] kinds)
    {
        var kind = element.GetProperty(property).ValueKind;
        if (!kinds.Contains(kind)) throw new CapabilityContractValidationException($"Property '{property}' has an invalid JSON type.");
    }
}
