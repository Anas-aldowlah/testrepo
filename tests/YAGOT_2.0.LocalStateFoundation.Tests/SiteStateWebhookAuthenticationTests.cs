using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateWebhookAuthenticationTests
{
    internal const string TestSecret =
        "0123456789abcdef0123456789abcdef";
    internal const string TestKeyId = "future-yagot-1";
    internal const long NowUnixSeconds = 1788516000;
    internal static readonly Guid DeliveryId =
        Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Fact]
    [Trait("Suite", "YAGOT02Helper")]
    public void KnownVector_MatchesCommittedControlPanelVector()
    {
        const string body = "{\"contractVersion\":1,\"siteId\":1}";

        var signature = Sign(NowUnixSeconds.ToString(), DeliveryId.ToString("D"),
            Encoding.UTF8.GetBytes(body));

        Assert.Equal(
            "sha256=18be0f06687d21a70400e396cce0c59bec95334733c0fb27bba1c54dde74d7af",
            signature);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Helper")]
    public void ValidRequest_AuthenticatesExactRawBytes()
    {
        var body = Encoding.UTF8.GetBytes("{ \"siteId\": 1 }");
        var headers = ValidHeaders(body);

        var result = CreateAuthenticator().Authenticate(headers, body);

        Assert.True(result.Succeeded);
        Assert.Equal(DeliveryId, result.DeliveryId);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("timestamp")]
    [InlineData("delivery")]
    [InlineData("key")]
    [Trait("Suite", "YAGOT02Helper")]
    public void MissingOrDuplicateHeader_IsRejected(string header)
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var headers = ValidHeaders(body);
        var name = HeaderName(header);
        headers.Remove(name);

        var missing = CreateAuthenticator().Authenticate(headers, body);
        headers[name] = new[] { "one", "two" };
        var duplicate = CreateAuthenticator().Authenticate(headers, body);

        Assert.Equal(
            SiteStateWebhookAuthenticationFailure.InvalidHeaderCardinality,
            missing.Failure);
        Assert.Equal(
            SiteStateWebhookAuthenticationFailure.InvalidHeaderCardinality,
            duplicate.Failure);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-300, true)]
    [InlineData(300, true)]
    [InlineData(-301, false)]
    [InlineData(301, false)]
    [Trait("Suite", "YAGOT02Helper")]
    public void TimestampTolerance_IsInclusive(int offset, bool accepted)
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var timestamp = (NowUnixSeconds + offset).ToString();
        var headers = ValidHeaders(body, timestamp: timestamp);

        var result = CreateAuthenticator().Authenticate(headers, body);

        Assert.Equal(accepted, result.Succeeded);
        if (!accepted)
        {
            Assert.Equal(
                SiteStateWebhookAuthenticationFailure.TimestampOutsideTolerance,
                result.Failure);
        }
    }

    [Theory]
    [InlineData("+1788516000")]
    [InlineData("01788516000")]
    [InlineData("1788516000 ")]
    [InlineData("-1")]
    [InlineData("1.788516E9")]
    [InlineData("999999999999999999999999")]
    [Trait("Suite", "YAGOT02Helper")]
    public void NoncanonicalTimestamp_IsRejected(string timestamp)
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var headers = ValidHeaders(body);
        headers[SiteStateWebhookAuthenticator.TimestampHeaderName] = timestamp;

        var result = CreateAuthenticator().Authenticate(headers, body);

        Assert.Equal(
            SiteStateWebhookAuthenticationFailure.MalformedTimestamp,
            result.Failure);
    }

    [Theory]
    [InlineData("00112233-4455-6677-8899-AABBCCDDEEFF")]
    [InlineData("{00112233-4455-6677-8899-aabbccddeeff}")]
    [InlineData("00112233445566778899aabbccddeeff")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [Trait("Suite", "YAGOT02Helper")]
    public void NoncanonicalOrEmptyDeliveryId_IsRejected(string deliveryId)
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var headers = ValidHeaders(body);
        headers[SiteStateWebhookAuthenticator.DeliveryIdHeaderName] = deliveryId;

        var result = CreateAuthenticator().Authenticate(headers, body);

        Assert.Equal(
            SiteStateWebhookAuthenticationFailure.MalformedDeliveryId,
            result.Failure);
    }

    [Theory]
    [InlineData("md5=18be0f06687d21a70400e396cce0c59bec95334733c0fb27bba1c54dde74d7af")]
    [InlineData("sha256=18BE0F06687D21A70400E396CCE0C59BEC95334733C0FB27BBA1C54DDE74D7AF")]
    [InlineData("sha256=abcd")]
    [InlineData("sha256=zzbe0f06687d21a70400e396cce0c59bec95334733c0fb27bba1c54dde74d7af")]
    [Trait("Suite", "YAGOT02Helper")]
    public void MalformedSignature_IsRejected(string signature)
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var headers = ValidHeaders(body);
        headers[SiteStateWebhookAuthenticator.SignatureHeaderName] = signature;

        var result = CreateAuthenticator().Authenticate(headers, body);

        Assert.Equal(
            SiteStateWebhookAuthenticationFailure.MalformedSignature,
            result.Failure);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Helper")]
    public void AlteredInputsAndWrongCredentials_FailAuthentication()
    {
        var body = Encoding.UTF8.GetBytes("{\"value\":1}");
        var originalHeaders = ValidHeaders(body);

        var alteredBody = Encoding.UTF8.GetBytes("{\"value\":1} ");
        AssertAuthenticationFailed(originalHeaders, alteredBody);

        var alteredTimestamp = Clone(originalHeaders);
        alteredTimestamp[SiteStateWebhookAuthenticator.TimestampHeaderName] =
            (NowUnixSeconds + 1).ToString();
        AssertAuthenticationFailed(alteredTimestamp, body);

        var alteredDelivery = Clone(originalHeaders);
        alteredDelivery[SiteStateWebhookAuthenticator.DeliveryIdHeaderName] =
            "11112233-4455-6677-8899-aabbccddeeff";
        AssertAuthenticationFailed(alteredDelivery, body);

        var alteredKey = Clone(originalHeaders);
        alteredKey[SiteStateWebhookAuthenticator.KeyIdHeaderName] = "wrong-key";
        AssertAuthenticationFailed(alteredKey, body);

        var wrongSecretAuthenticator = CreateAuthenticator(
            "fedcba9876543210fedcba9876543210");
        Assert.Equal(
            SiteStateWebhookAuthenticationFailure.AuthenticationFailed,
            wrongSecretAuthenticator.Authenticate(originalHeaders, body).Failure);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Helper")]
    public void WhitespaceAndPropertyOrder_ChangeSignatureAndHash()
    {
        var first = Encoding.UTF8.GetBytes("{\"a\":1,\"b\":2}");
        var whitespace = Encoding.UTF8.GetBytes("{ \"a\": 1, \"b\": 2 }");
        var reordered = Encoding.UTF8.GetBytes("{\"b\":2,\"a\":1}");

        Assert.NotEqual(SignBody(first), SignBody(whitespace));
        Assert.NotEqual(SignBody(first), SignBody(reordered));
        Assert.False(SHA256.HashData(first).SequenceEqual(
            SHA256.HashData(whitespace)));
        Assert.False(SHA256.HashData(first).SequenceEqual(
            SHA256.HashData(reordered)));
    }

    [Fact]
    [Trait("Suite", "YAGOT02Helper")]
    public async Task BoundedReader_AllowsLimitAndRejectsNextByte()
    {
        var atLimit = new byte[SiteStateWebhookOptions.BodySizeLimitBytes];
        var overLimit = new byte[SiteStateWebhookOptions.BodySizeLimitBytes + 1];

        var captured = await SiteStateWebhookBodyReader.ReadAsync(
            new MemoryStream(atLimit),
            SiteStateWebhookOptions.BodySizeLimitBytes,
            CancellationToken.None);

        Assert.Equal(atLimit.Length, captured.Length);
        await Assert.ThrowsAsync<SiteStateWebhookBodyTooLargeException>(() =>
            SiteStateWebhookBodyReader.ReadAsync(
                new MemoryStream(overLimit),
                SiteStateWebhookOptions.BodySizeLimitBytes,
                CancellationToken.None));
    }

    internal static HeaderDictionary ValidHeaders(
        byte[] body,
        string? timestamp = null,
        Guid? deliveryId = null)
    {
        var timestampText = timestamp ?? NowUnixSeconds.ToString();
        var deliveryText = (deliveryId ?? DeliveryId).ToString("D");
        return new HeaderDictionary
        {
            [SiteStateWebhookAuthenticator.TimestampHeaderName] = timestampText,
            [SiteStateWebhookAuthenticator.DeliveryIdHeaderName] = deliveryText,
            [SiteStateWebhookAuthenticator.KeyIdHeaderName] = TestKeyId,
            [SiteStateWebhookAuthenticator.SignatureHeaderName] =
                Sign(timestampText, deliveryText, body)
        };
    }

    internal static string Sign(string timestamp, string deliveryId, byte[] body)
    {
        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.{deliveryId}.");
        var input = new byte[prefix.Length + body.Length];
        prefix.CopyTo(input, 0);
        body.CopyTo(input, prefix.Length);
        return "sha256=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(TestSecret), input));
    }

    internal static SiteStateWebhookAuthenticator CreateAuthenticator(
        string secret = TestSecret) =>
        new(
            Options.Create(new SiteStateWebhookOptions
            {
                SiteId = 1,
                WebhookKeyId = TestKeyId,
                WebhookSecret = secret,
                TimestampToleranceSeconds = 300
            }),
            new FixedTimeProvider(
                DateTimeOffset.FromUnixTimeSeconds(NowUnixSeconds)));

    private static void AssertAuthenticationFailed(
        IHeaderDictionary headers,
        byte[] body) =>
        Assert.Equal(
            SiteStateWebhookAuthenticationFailure.AuthenticationFailed,
            CreateAuthenticator().Authenticate(headers, body).Failure);

    private static string SignBody(byte[] body) =>
        Sign(NowUnixSeconds.ToString(), DeliveryId.ToString("D"), body);

    private static HeaderDictionary Clone(IHeaderDictionary source)
    {
        var clone = new HeaderDictionary();
        foreach (var header in source)
        {
            clone[header.Key] = header.Value;
        }

        return clone;
    }

    private static string HeaderName(string key) => key switch
    {
        "signature" => SiteStateWebhookAuthenticator.SignatureHeaderName,
        "timestamp" => SiteStateWebhookAuthenticator.TimestampHeaderName,
        "delivery" => SiteStateWebhookAuthenticator.DeliveryIdHeaderName,
        "key" => SiteStateWebhookAuthenticator.KeyIdHeaderName,
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
