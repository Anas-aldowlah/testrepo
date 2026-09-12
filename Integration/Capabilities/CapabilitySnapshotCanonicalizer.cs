using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YAGOT_2._0.Integration.Capabilities;

public static class CapabilitySnapshotCanonicalizer
{
    public static string ToCanonicalJson(CapabilitySnapshotV1 snapshot)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("contractVersion", snapshot.ContractVersion);
            writer.WriteString("catalogVersion", snapshot.CatalogVersion);
            writer.WriteNumber("siteId", snapshot.SiteId);
            writer.WriteNumber("revision", snapshot.Revision);
            writer.WriteString("generatedAtUtc", snapshot.GeneratedAtUtc.ToUniversalTime().ToString("O"));
            writer.WriteString("effectiveAtUtc", snapshot.EffectiveAtUtc.ToUniversalTime().ToString("O"));
            writer.WriteStartArray("modules");
            foreach (var item in snapshot.Modules.OrderBy(x => x.Code, StringComparer.Ordinal))
            {
                writer.WriteStartObject(); writer.WriteString("code", item.Code); writer.WriteBoolean("enabled", item.Enabled); writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("features");
            foreach (var item in snapshot.Features.OrderBy(x => x.Code, StringComparer.Ordinal))
            {
                writer.WriteStartObject(); writer.WriteString("code", item.Code); writer.WriteString("moduleCode", item.ModuleCode); writer.WriteBoolean("enabled", item.Enabled); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static byte[] Hash(CapabilitySnapshotV1 snapshot) => SHA256.HashData(Encoding.UTF8.GetBytes(ToCanonicalJson(snapshot)));
}
