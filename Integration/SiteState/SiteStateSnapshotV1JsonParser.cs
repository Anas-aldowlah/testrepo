using System.Text.Json;
using System.Text.Json.Serialization;

namespace YAGOT_2._0.Integration.SiteState;

public enum SiteStateSnapshotParseFailure
{
    None,
    MalformedJson,
    InvalidContract
}

public sealed record SiteStateSnapshotParseResult(
    SiteStateSnapshotV1? Snapshot,
    SiteStateSnapshotParseFailure Failure)
{
    public static SiteStateSnapshotParseResult Success(
        SiteStateSnapshotV1 snapshot) =>
        new(snapshot, SiteStateSnapshotParseFailure.None);

    public static SiteStateSnapshotParseResult Failed(
        SiteStateSnapshotParseFailure failure) =>
        new(null, failure);
}

public interface ISiteStateSnapshotV1JsonParser
{
    SiteStateSnapshotParseResult Parse(ReadOnlyMemory<byte> rawBody);
}

public sealed class SiteStateSnapshotV1JsonParser :
    ISiteStateSnapshotV1JsonParser
{
    private static readonly HashSet<string> RequiredProperties =
    [
        "contractVersion",
        "siteId",
        "mode",
        "revision",
        "effectiveAtUtc",
        "expiresAtUtc",
        "siteName",
        "siteUrl",
        "startDate",
        "originalDurationDays"
    ];

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            NumberHandling = JsonNumberHandling.Strict,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow
        };

    public SiteStateSnapshotParseResult Parse(ReadOnlyMemory<byte> rawBody)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawBody, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });
        }
        catch (JsonException)
        {
            return SiteStateSnapshotParseResult.Failed(
                SiteStateSnapshotParseFailure.MalformedJson);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return SiteStateSnapshotParseResult.Failed(
                    SiteStateSnapshotParseFailure.InvalidContract);
            }

            var encounteredProperties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!RequiredProperties.Contains(property.Name) ||
                    !encounteredProperties.Add(property.Name))
                {
                    return SiteStateSnapshotParseResult.Failed(
                        SiteStateSnapshotParseFailure.InvalidContract);
                }
            }

            if (encounteredProperties.Count != RequiredProperties.Count)
            {
                return SiteStateSnapshotParseResult.Failed(
                    SiteStateSnapshotParseFailure.InvalidContract);
            }
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<SiteStateSnapshotV1>(
                rawBody.Span,
                SerializerOptions);
            return snapshot is null
                ? SiteStateSnapshotParseResult.Failed(
                    SiteStateSnapshotParseFailure.InvalidContract)
                : SiteStateSnapshotParseResult.Success(snapshot);
        }
        catch (JsonException)
        {
            return SiteStateSnapshotParseResult.Failed(
                SiteStateSnapshotParseFailure.InvalidContract);
        }
        catch (NotSupportedException)
        {
            return SiteStateSnapshotParseResult.Failed(
                SiteStateSnapshotParseFailure.InvalidContract);
        }
    }
}
