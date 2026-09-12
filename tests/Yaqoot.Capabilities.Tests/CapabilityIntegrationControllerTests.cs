using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Controllers.Api;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class CapabilityIntegrationControllerTests
{
    private const string WebhookKeyId = "capability-test-key";
    private const string WebhookSecret =
        "capability-test-secret-at-least-32-bytes";

    private readonly CapabilityCatalog _catalog = new();
    private readonly JsonSerializerOptions _json =
        new(JsonSerializerDefaults.Web);
    private readonly DateTimeOffset _now =
        new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidSignedSnapshot_IsApplied()
    {
        var apply = Apply(CapabilityApplyOutcome.Applied);

        var response = await SendAsync(Serialize(Create()), apply);

        AssertResponse(response, StatusCodes.Status200OK, "applied");
        Assert.Equal(1, apply.Calls);
        Assert.Equal(1, apply.Snapshot!.SiteId);
        Assert.NotEqual(Guid.Empty, apply.Delivery!.DeliveryId);
    }

    [Fact]
    public async Task InvalidSignature_IsUnauthorizedBeforeApply()
    {
        var apply = Apply(CapabilityApplyOutcome.Applied);

        var response = await SendAsync(
            Serialize(Create()),
            apply,
            signatureOverride: $"sha256={new string('0', 64)}");

        AssertResponse(
            response,
            StatusCodes.Status401Unauthorized,
            "authentication_failed");
        Assert.Equal(0, apply.Calls);
    }

    [Fact]
    public async Task MissingSignatureHeaders_IsRejectedBeforeApply()
    {
        var apply = Apply(CapabilityApplyOutcome.Applied);

        var response = await SendAsync(
            Serialize(Create()),
            apply,
            includeSignatureHeaders: false);

        AssertResponse(response, StatusCodes.Status400BadRequest, "invalid_headers");
        Assert.Equal(0, apply.Calls);
    }

    [Fact]
    public async Task MalformedJson_IsBadRequestBeforeApply()
    {
        var apply = Apply(CapabilityApplyOutcome.Applied);

        var response = await SendAsync("{not-json", apply);

        AssertResponse(response, StatusCodes.Status400BadRequest, "malformed_json");
        Assert.Equal(0, apply.Calls);
    }

    [Fact]
    public async Task UnknownProperty_IsBadRequestBeforeApply()
    {
        var apply = Apply(CapabilityApplyOutcome.Applied);
        var json = Serialize(Create()).Insert(1, "\"unexpected\":true,");

        var response = await SendAsync(json, apply);

        AssertResponse(response, StatusCodes.Status400BadRequest, "invalid_contract");
        Assert.Equal(0, apply.Calls);
    }

    [Fact]
    public async Task UnsupportedContractVersion_IsBadRequestBeforeApply()
    {
        var apply = Apply(CapabilityApplyOutcome.Applied);

        var response = await SendAsync(
            Serialize(Create() with { ContractVersion = 2 }),
            apply);

        AssertResponse(response, StatusCodes.Status400BadRequest, "invalid_contract");
        Assert.Equal(0, apply.Calls);
    }

    [Fact]
    public async Task UnsupportedCatalogVersion_IsBadRequestBeforeApply()
    {
        var apply = Apply(CapabilityApplyOutcome.Applied);

        var response = await SendAsync(
            Serialize(Create() with { CatalogVersion = "unsupported" }),
            apply);

        AssertResponse(response, StatusCodes.Status400BadRequest, "invalid_contract");
        Assert.Equal(0, apply.Calls);
    }

    [Fact]
    public async Task WrongSiteId_IsBadRequestBeforeApply()
    {
        var apply = Apply(CapabilityApplyOutcome.Applied);

        var response = await SendAsync(
            Serialize(Create() with { SiteId = 2 }),
            apply);

        AssertResponse(response, StatusCodes.Status400BadRequest, "invalid_contract");
        Assert.Equal(0, apply.Calls);
    }

    [Theory]
    [InlineData(CapabilityApplyOutcome.Stale, "stale")]
    [InlineData(CapabilityApplyOutcome.Equal, "equal")]
    public async Task StaleOrEqualSnapshot_IsIdempotentSuccess(
        CapabilityApplyOutcome outcome,
        string code)
    {
        var response = await SendAsync(
            Serialize(Create()),
            Apply(outcome));

        AssertResponse(response, StatusCodes.Status200OK, code);
    }

    [Fact]
    public async Task EqualRevisionWithDifferentHash_IsConflict()
    {
        var response = await SendAsync(
            Serialize(Create()),
            Apply(
                CapabilityApplyOutcome.EqualConflict,
                CapabilityHealthStatus.RevisionConflict));

        AssertResponse(response, StatusCodes.Status409Conflict, "equal_conflict");
    }

    [Fact]
    public async Task DuplicateDeliveryWithSamePayload_IsIdempotentSuccess()
    {
        var response = await SendAsync(
            Serialize(Create()),
            Apply(
                CapabilityApplyOutcome.DuplicateDelivery,
                original: CapabilityReceiptDecision.Applied));

        AssertResponse(
            response,
            StatusCodes.Status200OK,
            "duplicate_delivery");
    }

    [Fact]
    public async Task DuplicateDeliveryWithDifferentPayload_IsConflict()
    {
        var response = await SendAsync(
            Serialize(Create()),
            Apply(
                CapabilityApplyOutcome.Conflict,
                CapabilityHealthStatus.RevisionConflict,
                CapabilityReceiptDecision.Applied));

        AssertResponse(
            response,
            StatusCodes.Status409Conflict,
            "delivery_id_payload_conflict");
    }

    [Fact]
    public async Task DisabledM03Snapshot_ReachesApplyService()
    {
        var apply = Apply(CapabilityApplyOutcome.Applied);

        var response = await SendAsync(Serialize(DisabledM03()), apply);

        AssertResponse(response, StatusCodes.Status200OK, "applied");
        Assert.False(apply.Snapshot!.Modules.Single(
            module => module.Code == CapabilityModuleCodes.Categories).Enabled);
    }

    [Fact]
    public async Task RuntimeStateBecomesM03DisabledAfterSuccessfulApply()
    {
        var runtime = new LocalCapabilityRuntimeStateProvider(
            _catalog,
            new FixedTimeProvider(_now));
        var apply = Apply(CapabilityApplyOutcome.Applied, publisher: runtime);

        var response = await SendAsync(Serialize(DisabledM03()), apply);

        AssertResponse(response, StatusCodes.Status200OK, "applied");
        Assert.False(runtime.IsModuleEnabled(CapabilityModuleCodes.Categories));
        Assert.Equal(1, runtime.Current!.Revision);
        Assert.Equal(CapabilityHealthStatus.Healthy, runtime.Health);
    }

    [Fact]
    public async Task UnsupportedContentType_IsRejectedBeforeAuthenticationAndApply()
    {
        var apply = Apply(CapabilityApplyOutcome.Applied);

        var response = await SendAsync(
            Serialize(Create()),
            apply,
            contentType: "text/plain");

        AssertResponse(
            response,
            StatusCodes.Status415UnsupportedMediaType,
            "unsupported_media_type");
        Assert.Equal(0, apply.Calls);
    }

    private async Task<IActionResult> SendAsync(
        string json,
        RecordingApplyService apply,
        bool includeSignatureHeaders = true,
        string? signatureOverride = null,
        string contentType = "application/json; charset=utf-8")
    {
        var body = Encoding.UTF8.GetBytes(json);
        var timestamp = _now.ToUnixTimeSeconds().ToString();
        var deliveryId = Guid.NewGuid().ToString("D");
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;
        context.Request.ContentType = contentType;

        if (includeSignatureHeaders)
        {
            context.Request.Headers[SiteStateWebhookAuthenticator.TimestampHeaderName] =
                timestamp;
            context.Request.Headers[SiteStateWebhookAuthenticator.DeliveryIdHeaderName] =
                deliveryId;
            context.Request.Headers[SiteStateWebhookAuthenticator.KeyIdHeaderName] =
                WebhookKeyId;
            context.Request.Headers[SiteStateWebhookAuthenticator.SignatureHeaderName] =
                signatureOverride ?? Sign(timestamp, deliveryId, body);
        }

        var controller = new CapabilityIntegrationController(
            Authenticator(),
            new CapabilitySnapshotV1JsonParser(
                _catalog,
                Options.Create(new CapabilityIntegrationOptions { SiteId = 1 })),
            apply,
            NullLogger<CapabilityIntegrationController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        return await controller.Receive(CancellationToken.None);
    }

    private SiteStateWebhookAuthenticator Authenticator() =>
        new(
            Options.Create(new SiteStateWebhookOptions
            {
                SiteId = 1,
                WebhookKeyId = WebhookKeyId,
                WebhookSecret = WebhookSecret,
                TimestampToleranceSeconds =
                    SiteStateWebhookOptions.ApprovedTimestampToleranceSeconds
            }),
            new FixedTimeProvider(_now));

    private static string Sign(
        string timestamp,
        string deliveryId,
        byte[] rawBody)
    {
        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.{deliveryId}.");
        var input = new byte[prefix.Length + rawBody.Length];
        prefix.CopyTo(input, 0);
        rawBody.CopyTo(input, prefix.Length);
        var digest = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(WebhookSecret),
            input);
        return $"sha256={Convert.ToHexStringLower(digest)}";
    }

    private CapabilitySnapshotV1 Create()
    {
        var at = new DateTimeOffset(2026, 9, 12, 9, 55, 0, TimeSpan.Zero);
        return new(
            CapabilityContractV1.ContractVersion,
            CapabilityContractV1.CatalogVersion,
            1,
            1,
            at,
            at,
            _catalog.Modules.Select(module =>
                new CapabilityModuleStateV1(module.Code, true)).ToArray(),
            _catalog.Features.Select(feature =>
                new CapabilityFeatureStateV1(
                    feature.Code,
                    feature.ModuleCode,
                    true)).ToArray());
    }

    private CapabilitySnapshotV1 DisabledM03()
    {
        var snapshot = Create();
        return snapshot with
        {
            Modules = snapshot.Modules.Select(module =>
                module.Code == CapabilityModuleCodes.Categories
                    ? module with { Enabled = false }
                    : module).ToArray()
        };
    }

    private string Serialize(CapabilitySnapshotV1 snapshot) =>
        JsonSerializer.Serialize(snapshot, _json);

    private static RecordingApplyService Apply(
        CapabilityApplyOutcome outcome,
        CapabilityHealthStatus health = CapabilityHealthStatus.Healthy,
        CapabilityReceiptDecision? original = null,
        ICapabilityRuntimePublisher? publisher = null) =>
        new(new CapabilityApplyResult(outcome, health, original), publisher);

    private static void AssertResponse(
        IActionResult result,
        int expectedStatus,
        string expectedCode)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        var response = Assert.IsType<CapabilityWebhookResponse>(objectResult.Value);
        Assert.Equal(expectedCode, response.Code);
        Assert.Equal(expectedStatus is >= 200 and < 300, response.Success);
    }

    private sealed class RecordingApplyService(
        CapabilityApplyResult result,
        ICapabilityRuntimePublisher? publisher = null) : ICapabilityApplyService
    {
        public int Calls { get; private set; }
        public CapabilitySnapshotV1? Snapshot { get; private set; }
        public CapabilityDeliveryContext? Delivery { get; private set; }

        public Task<CapabilityApplyResult> ApplyAsync(
            CapabilitySnapshotV1 snapshot,
            CapabilityDeliveryContext? delivery = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Snapshot = snapshot;
            Delivery = delivery;
            if (result.Outcome == CapabilityApplyOutcome.Applied)
            {
                publisher?.Publish(snapshot);
            }

            return Task.FromResult(result);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
