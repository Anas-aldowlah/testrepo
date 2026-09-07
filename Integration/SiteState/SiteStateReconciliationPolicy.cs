using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace YAGOT_2._0.Integration.SiteState;

public interface ISiteStateReconciliationPolicy
{
    TimeSpan GetStartupDelay();
    TimeSpan GetSuccessfulCycleDelay();
    TimeSpan GetFailedCycleDelay(int consecutiveFailures);
    TimeSpan? GetInlineRetryDelay(int nextAttempt, TimeSpan? retryAfter);
}

public sealed class SiteStateReconciliationPolicy :
    ISiteStateReconciliationPolicy
{
    private static readonly TimeSpan MaximumInlineRetryAfter =
        TimeSpan.FromMinutes(5);
    private readonly TimeSpan _baseInterval;

    public SiteStateReconciliationPolicy(
        IOptions<SiteStateReconciliationOptions> options)
    {
        _baseInterval = TimeSpan.FromMinutes(
            options.Value.ReconciliationIntervalMinutes);
    }

    public TimeSpan GetStartupDelay() =>
        RandomBetween(TimeSpan.Zero, TimeSpan.FromSeconds(30));

    public TimeSpan GetSuccessfulCycleDelay() =>
        _baseInterval +
        RandomBetween(TimeSpan.Zero, TimeSpan.FromMinutes(5));

    public TimeSpan GetFailedCycleDelay(int consecutiveFailures)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(consecutiveFailures);

        var baseDelay = consecutiveFailures switch
        {
            1 => _baseInterval,
            2 => _baseInterval * 2,
            _ => _baseInterval * 4
        };
        return baseDelay + RandomBetween(
            TimeSpan.Zero,
            TimeSpan.FromMinutes(5));
    }

    public TimeSpan? GetInlineRetryDelay(
        int nextAttempt,
        TimeSpan? retryAfter)
    {
        var randomDelay = nextAttempt switch
        {
            2 => RandomBetween(
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5)),
            3 => RandomBetween(
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(nextAttempt),
                nextAttempt,
                "Only attempts two and three are retry attempts.")
        };

        if (retryAfter.HasValue &&
            retryAfter.Value > MaximumInlineRetryAfter)
        {
            return null;
        }

        return retryAfter is { } minimum && minimum > randomDelay
            ? minimum
            : randomDelay;
    }

    private static TimeSpan RandomBetween(TimeSpan minimum, TimeSpan maximum)
    {
        var rangeMilliseconds = checked(
            (int)(maximum - minimum).TotalMilliseconds);
        if (rangeMilliseconds == 0)
        {
            return minimum;
        }

        return minimum + TimeSpan.FromMilliseconds(
            RandomNumberGenerator.GetInt32(rangeMilliseconds + 1));
    }
}
