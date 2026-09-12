using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Controllers.Api;

[ApiController]
[Route(CapabilityWebhookRoute.AttributePattern)]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[DisableCors]
public sealed class CapabilityIntegrationController(
    ISiteStateWebhookAuthenticator authenticator,
    ICapabilitySnapshotV1JsonParser parser,
    ICapabilityApplyService applyService,
    ILogger<CapabilityIntegrationController> logger) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(SiteStateWebhookOptions.BodySizeLimitBytes)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";

        try
        {
            return await ReceiveCore(cancellationToken);
        }
        catch (OperationCanceledException) when (
            Request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsTemporaryStorageFailure(exception))
        {
            logger.LogError(
                "Capability webhook failed before apply; category TemporaryStorageFailure; failure type {FailureType}.",
                exception.GetType().Name);
            return WebhookStatus(
                StatusCodes.Status503ServiceUnavailable,
                "temporary_storage_failure");
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Capability webhook failed before apply; category UnexpectedFailure; failure type {FailureType}.",
                exception.GetType().Name);
            return WebhookStatus(
                StatusCodes.Status500InternalServerError,
                "server_error");
        }
    }

    private async Task<IActionResult> ReceiveCore(CancellationToken cancellationToken)
    {
        if (!HasSupportedMediaType(Request) ||
            Request.Headers.ContentEncoding.Count != 0)
        {
            return WebhookStatus(
                StatusCodes.Status415UnsupportedMediaType,
                "unsupported_media_type");
        }

        if (Request.ContentLength is > SiteStateWebhookOptions.BodySizeLimitBytes)
        {
            return WebhookStatus(
                StatusCodes.Status413PayloadTooLarge,
                "payload_too_large");
        }

        byte[] rawBody;
        try
        {
            rawBody = await SiteStateWebhookBodyReader.ReadAsync(
                Request.Body,
                SiteStateWebhookOptions.BodySizeLimitBytes,
                cancellationToken);
        }
        catch (SiteStateWebhookBodyTooLargeException)
        {
            return WebhookStatus(
                StatusCodes.Status413PayloadTooLarge,
                "payload_too_large");
        }
        catch (BadHttpRequestException exception) when (
            exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return WebhookStatus(
                StatusCodes.Status413PayloadTooLarge,
                "payload_too_large");
        }

        var authentication = authenticator.Authenticate(Request.Headers, rawBody);
        if (!authentication.Succeeded)
        {
            logger.LogWarning(
                "Capability webhook rejected; category {FailureCategory}.",
                authentication.Failure);
            return AuthenticationFailure(authentication.Failure);
        }

        var parsed = parser.Parse(rawBody);
        if (parsed.Failure == CapabilitySnapshotParseFailure.MalformedJson)
        {
            return WebhookStatus(
                StatusCodes.Status400BadRequest,
                "malformed_json");
        }

        if (parsed.Failure != CapabilitySnapshotParseFailure.None ||
            parsed.Snapshot is null)
        {
            return WebhookStatus(
                StatusCodes.Status400BadRequest,
                "invalid_contract");
        }

        var snapshot = parsed.Snapshot;
        var result = await applyService.ApplyAsync(
            snapshot,
            new CapabilityDeliveryContext(authentication.DeliveryId),
            cancellationToken);

        var statusCode = ApplyStatusCode(result);
        logger.LogInformation(
            "Capability webhook completed for delivery {DeliveryId}, site {SiteId}, revision {Revision}; outcome {Outcome}; HTTP {StatusCode}.",
            authentication.DeliveryId,
            snapshot.SiteId,
            snapshot.Revision,
            result.Outcome,
            statusCode);

        return WebhookStatus(
            statusCode,
            OutcomeCode(result),
            result.Outcome.ToString());
    }

    private IActionResult AuthenticationFailure(
        SiteStateWebhookAuthenticationFailure failure) =>
        failure switch
        {
            SiteStateWebhookAuthenticationFailure.InvalidHeaderCardinality =>
                WebhookStatus(StatusCodes.Status400BadRequest, "invalid_headers"),
            SiteStateWebhookAuthenticationFailure.MalformedTimestamp =>
                WebhookStatus(StatusCodes.Status400BadRequest, "malformed_timestamp"),
            SiteStateWebhookAuthenticationFailure.TimestampOutsideTolerance =>
                WebhookStatus(StatusCodes.Status401Unauthorized, "timestamp_outside_tolerance"),
            SiteStateWebhookAuthenticationFailure.MalformedDeliveryId =>
                WebhookStatus(StatusCodes.Status400BadRequest, "malformed_delivery_id"),
            SiteStateWebhookAuthenticationFailure.MalformedSignature =>
                WebhookStatus(StatusCodes.Status400BadRequest, "malformed_signature"),
            SiteStateWebhookAuthenticationFailure.AuthenticationFailed =>
                WebhookStatus(StatusCodes.Status401Unauthorized, "authentication_failed"),
            _ => WebhookStatus(
                StatusCodes.Status500InternalServerError,
                "server_error")
        };

    private ObjectResult WebhookStatus(
        int statusCode,
        string code,
        string? outcome = null) =>
        StatusCode(
            statusCode,
            new CapabilityWebhookResponse(
                statusCode is >= 200 and < 300,
                code,
                outcome));

    private static int ApplyStatusCode(CapabilityApplyResult result) =>
        result.Outcome switch
        {
            CapabilityApplyOutcome.EqualConflict or
                CapabilityApplyOutcome.Conflict => StatusCodes.Status409Conflict,
            CapabilityApplyOutcome.DuplicateDelivery
                when result.OriginalDeliveryDecision ==
                     CapabilityReceiptDecision.EqualConflict =>
                StatusCodes.Status409Conflict,
            CapabilityApplyOutcome.Applied or
                CapabilityApplyOutcome.Equal or
                CapabilityApplyOutcome.Stale or
                CapabilityApplyOutcome.DuplicateDelivery => StatusCodes.Status200OK,
            CapabilityApplyOutcome.Rejected
                when result.Health == CapabilityHealthStatus.InvalidSnapshot =>
                StatusCodes.Status400BadRequest,
            CapabilityApplyOutcome.Rejected
                when result.Health == CapabilityHealthStatus.StorageFailure =>
                StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

    private static string OutcomeCode(CapabilityApplyResult result) =>
        result.Outcome switch
        {
            CapabilityApplyOutcome.Applied => "applied",
            CapabilityApplyOutcome.Equal => "equal",
            CapabilityApplyOutcome.Stale => "stale",
            CapabilityApplyOutcome.DuplicateDelivery => "duplicate_delivery",
            CapabilityApplyOutcome.EqualConflict => "equal_conflict",
            CapabilityApplyOutcome.Conflict => "delivery_id_payload_conflict",
            CapabilityApplyOutcome.Rejected
                when result.Health == CapabilityHealthStatus.InvalidSnapshot =>
                "invalid_contract",
            CapabilityApplyOutcome.Rejected
                when result.Health == CapabilityHealthStatus.StorageFailure =>
                "temporary_storage_failure",
            _ => "server_error"
        };

    private static bool HasSupportedMediaType(HttpRequest request)
    {
        if (!MediaTypeHeaderValue.TryParse(
                request.ContentType,
                out var contentType) ||
            !string.Equals(
                contentType.MediaType.Value,
                "application/json",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var charset = HeaderUtilities.RemoveQuotes(contentType.Charset).Value;
        return string.IsNullOrEmpty(charset) ||
               string.Equals(charset, "utf-8", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemporaryStorageFailure(Exception exception) =>
        exception is DbException or DbUpdateException or TimeoutException or
            OperationCanceledException ||
        exception.InnerException is DbException or TimeoutException;
}
