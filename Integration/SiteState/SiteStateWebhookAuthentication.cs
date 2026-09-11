using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace YAGOT_2._0.Integration.SiteState;

public enum SiteStateWebhookAuthenticationFailure
{
    None,
    InvalidHeaderCardinality,
    MalformedTimestamp,
    TimestampOutsideTolerance,
    MalformedDeliveryId,
    AuthenticationFailed,
    MalformedSignature
}

public sealed record SiteStateWebhookAuthenticationResult(
    bool Succeeded,
    Guid DeliveryId,
    SiteStateWebhookAuthenticationFailure Failure)
{
    public static SiteStateWebhookAuthenticationResult Success(Guid deliveryId) =>
        new(true, deliveryId, SiteStateWebhookAuthenticationFailure.None);

    public static SiteStateWebhookAuthenticationResult Failed(
        SiteStateWebhookAuthenticationFailure failure) =>
        new(false, Guid.Empty, failure);
}

public interface ISiteStateWebhookAuthenticator
{
    SiteStateWebhookAuthenticationResult Authenticate(
        IHeaderDictionary headers,
        ReadOnlyMemory<byte> rawBody);
}

public sealed class SiteStateWebhookAuthenticator : ISiteStateWebhookAuthenticator
{
    public const string SignatureHeaderName = "X-Yagot-Signature";
    public const string TimestampHeaderName = "X-Yagot-Timestamp";
    public const string DeliveryIdHeaderName = "X-Yagot-Delivery-Id";
    public const string KeyIdHeaderName = "X-Yagot-Key-Id";

    private const string SignaturePrefix = "sha256=";

    private readonly SiteStateWebhookOptions _options;
    private readonly TimeProvider _timeProvider;

    public SiteStateWebhookAuthenticator(
        IOptions<SiteStateWebhookOptions> options,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public SiteStateWebhookAuthenticationResult Authenticate(
        IHeaderDictionary headers,
        ReadOnlyMemory<byte> rawBody)
    {
        if (!TryGetSingle(headers, SignatureHeaderName, out var signature) ||
            !TryGetSingle(headers, TimestampHeaderName, out var timestampText) ||
            !TryGetSingle(headers, DeliveryIdHeaderName, out var deliveryIdText) ||
            !TryGetSingle(headers, KeyIdHeaderName, out var keyId))
        {
            return SiteStateWebhookAuthenticationResult.Failed(
                SiteStateWebhookAuthenticationFailure.InvalidHeaderCardinality);
        }

        if (!IsCanonicalTimestamp(timestampText) ||
            !long.TryParse(
                timestampText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var timestamp))
        {
            return SiteStateWebhookAuthenticationResult.Failed(
                SiteStateWebhookAuthenticationFailure.MalformedTimestamp);
        }

        var now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var tolerance = _options.TimestampToleranceSeconds;
        if (timestamp < now - tolerance || timestamp > now + tolerance)
        {
            return SiteStateWebhookAuthenticationResult.Failed(
                SiteStateWebhookAuthenticationFailure.TimestampOutsideTolerance);
        }

        if (!Guid.TryParseExact(deliveryIdText, "D", out var deliveryId) ||
            deliveryId == Guid.Empty ||
            !string.Equals(
                deliveryId.ToString("D"),
                deliveryIdText,
                StringComparison.Ordinal))
        {
            return SiteStateWebhookAuthenticationResult.Failed(
                SiteStateWebhookAuthenticationFailure.MalformedDeliveryId);
        }

        if (!string.Equals(keyId, _options.WebhookKeyId, StringComparison.Ordinal))
        {
            return SiteStateWebhookAuthenticationResult.Failed(
                SiteStateWebhookAuthenticationFailure.AuthenticationFailed);
        }

        if (!TryDecodeSignature(signature, out var suppliedDigest))
        {
            return SiteStateWebhookAuthenticationResult.Failed(
                SiteStateWebhookAuthenticationFailure.MalformedSignature);
        }

        var prefixBytes = Encoding.UTF8.GetBytes(
            $"{timestampText}.{deliveryIdText}.");
        var signatureInput = GC.AllocateUninitializedArray<byte>(
            prefixBytes.Length + rawBody.Length);
        prefixBytes.CopyTo(signatureInput, 0);
        rawBody.Span.CopyTo(signatureInput.AsSpan(prefixBytes.Length));

        var secretBytes = Encoding.UTF8.GetBytes(_options.WebhookSecret!);
        try
        {
            var expectedDigest = HMACSHA256.HashData(secretBytes, signatureInput);
            return CryptographicOperations.FixedTimeEquals(
                suppliedDigest,
                expectedDigest)
                ? SiteStateWebhookAuthenticationResult.Success(deliveryId)
                : SiteStateWebhookAuthenticationResult.Failed(
                    SiteStateWebhookAuthenticationFailure.AuthenticationFailed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            CryptographicOperations.ZeroMemory(signatureInput);
        }
    }

    private static bool TryGetSingle(
        IHeaderDictionary headers,
        string name,
        out string value)
    {
        value = string.Empty;
        if (!headers.TryGetValue(name, out StringValues values) ||
            values.Count != 1 ||
            string.IsNullOrEmpty(values[0]))
        {
            return false;
        }

        value = values[0]!;
        return true;
    }

    private static bool IsCanonicalTimestamp(string value)
    {
        if (value.Length == 0 || value.Length > 19 ||
            value.Length > 1 && value[0] == '0')
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryDecodeSignature(
        string signature,
        out byte[] digest)
    {
        digest = [];
        if (signature.Length != SignaturePrefix.Length + 64 ||
            !signature.StartsWith(SignaturePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var hex = signature.AsSpan(SignaturePrefix.Length);
        foreach (var character in hex)
        {
            if (character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        digest = Convert.FromHexString(hex);
        return true;
    }
}
