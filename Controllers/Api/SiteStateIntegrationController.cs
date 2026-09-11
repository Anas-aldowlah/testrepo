using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;

namespace YAGOT_2._0.Controllers.Api;

[ApiController]
[Route(SiteStateWebhookRoute.AttributePattern)]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[DisableCors]
public sealed class SiteStateIntegrationController : ControllerBase
{
    private readonly ISiteStateWebhookAuthenticator _authenticator;
    private readonly ISiteStateSnapshotV1JsonParser _parser;
    private readonly ISiteStateApplyService _applyService;
    private readonly SiteStateWebhookOptions _options;
    private readonly ILogger<SiteStateIntegrationController> _logger;

    public SiteStateIntegrationController(
        ISiteStateWebhookAuthenticator authenticator,
        ISiteStateSnapshotV1JsonParser parser,
        ISiteStateApplyService applyService,
        IOptions<SiteStateWebhookOptions> options,
        ILogger<SiteStateIntegrationController> logger)
    {
        _authenticator = authenticator;
        _parser = parser;
        _applyService = applyService;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(SiteStateWebhookOptions.BodySizeLimitBytes)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        try
        {
            return await ReceiveCore(cancellationToken);
        }
        catch (OperationCanceledException) when (Request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsTemporaryStorageFailure(exception))
        {
            _logger.LogError(
                "Site-state webhook failed before apply; category TemporaryStorageFailure; failure type {FailureType}.",
                exception.GetType().Name);
            return WebhookStatus(
                StatusCodes.Status503ServiceUnavailable,
                "temporary_storage_failure");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Site-state webhook failed before apply; category UnexpectedFailure; failure type {FailureType}.",
                exception.GetType().Name);
            return WebhookStatus(
                StatusCodes.Status500InternalServerError,
                "server_error");
        }
    }

    private async Task<IActionResult> ReceiveCore(
        CancellationToken cancellationToken)
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

        var authentication = _authenticator.Authenticate(
            Request.Headers,
            rawBody);
        if (!authentication.Succeeded)
        {
            _logger.LogWarning(
                "Site-state webhook rejected; category {FailureCategory}.",
                authentication.Failure);
            return AuthenticationFailure(authentication.Failure);
        }

        var parsed = _parser.Parse(rawBody);
        if (parsed.Failure == SiteStateSnapshotParseFailure.MalformedJson)
        {
            return WebhookStatus(
                StatusCodes.Status400BadRequest,
                "malformed_json");
        }

        if (parsed.Failure != SiteStateSnapshotParseFailure.None ||
            parsed.Snapshot is null)
        {
            return WebhookStatus(
                StatusCodes.Status422UnprocessableEntity,
                "invalid_contract");
        }

        var snapshot = parsed.Snapshot;
        if (snapshot.ContractVersion != SiteStateContractV1.ContractVersion)
        {
            return WebhookStatus(
                StatusCodes.Status422UnprocessableEntity,
                "unsupported_contract_version");
        }

        if (snapshot.SiteId != _options.SiteId)
        {
            return WebhookStatus(
                StatusCodes.Status403Forbidden,
                "wrong_site");
        }

        try
        {
            snapshot.Validate();
        }
        catch (SiteStateContractValidationException)
        {
            return WebhookStatus(
                StatusCodes.Status422UnprocessableEntity,
                "invalid_contract");
        }

        var delivery = new SiteStateDeliveryContext(
            authentication.DeliveryId,
            SHA256.HashData(rawBody));

        try
        {
            var result = await _applyService.ApplyAsync(
                snapshot,
                delivery,
                cancellationToken);
            var statusCode = result.Outcome switch
            {
                SiteStateApplyOutcome.EqualConflict =>
                    StatusCodes.Status409Conflict,
                SiteStateApplyOutcome.DeliveryIdPayloadConflict =>
                    StatusCodes.Status409Conflict,
                SiteStateApplyOutcome.DuplicateDelivery
                    when result.OriginalDeliveryDecision ==
                         SiteStateReceiptDecision.EqualConflict =>
                    StatusCodes.Status409Conflict,
                SiteStateApplyOutcome.Applied or
                    SiteStateApplyOutcome.Equal or
                    SiteStateApplyOutcome.Stale or
                    SiteStateApplyOutcome.DuplicateDelivery =>
                    StatusCodes.Status200OK,
                _ => throw new InvalidOperationException(
                    "The site-state apply service returned an unsupported outcome.")
            };

            _logger.LogInformation(
                "Site-state webhook completed for delivery {DeliveryId}, site {SiteId}, revision {Revision}; outcome {Outcome}; HTTP {StatusCode}.",
                authentication.DeliveryId,
                snapshot.SiteId,
                snapshot.Revision,
                result.Outcome,
                statusCode);

            return WebhookStatus(
                statusCode,
                OutcomeCode(result.Outcome),
                result.Outcome.ToString());
        }
        catch (OperationCanceledException) when (Request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsTemporaryStorageFailure(exception))
        {
            _logger.LogError(
                "Site-state webhook storage failed for delivery {DeliveryId}, site {SiteId}, revision {Revision}; failure type {FailureType}.",
                authentication.DeliveryId,
                snapshot.SiteId,
                snapshot.Revision,
                exception.GetType().Name);
            return WebhookStatus(
                StatusCodes.Status503ServiceUnavailable,
                "temporary_storage_failure");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Site-state webhook failed for delivery {DeliveryId}, site {SiteId}, revision {Revision}; failure type {FailureType}.",
                authentication.DeliveryId,
                snapshot.SiteId,
                snapshot.Revision,
                exception.GetType().Name);
            return WebhookStatus(
                StatusCodes.Status500InternalServerError,
                "server_error");
        }
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
            new SiteStateWebhookResponse(
                statusCode is >= 200 and < 300,
                code,
                outcome));

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

    private static string OutcomeCode(SiteStateApplyOutcome outcome) =>
        outcome switch
        {
            SiteStateApplyOutcome.Applied => "applied",
            SiteStateApplyOutcome.Equal => "equal",
            SiteStateApplyOutcome.Stale => "stale",
            SiteStateApplyOutcome.DuplicateDelivery => "duplicate_delivery",
            SiteStateApplyOutcome.EqualConflict => "equal_conflict",
            SiteStateApplyOutcome.DeliveryIdPayloadConflict =>
                "delivery_id_payload_conflict",
            _ => "server_error"
        };
}
