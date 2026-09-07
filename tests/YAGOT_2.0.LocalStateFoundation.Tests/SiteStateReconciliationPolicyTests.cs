using YAGOT_2._0.Integration.SiteState;
using Microsoft.Extensions.Options;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateReconciliationPolicyTests
{
    [Fact]
    public void DelaysRemainWithinApprovedBounds()
    {
        var policy = Policy();

        for (var index = 0; index < 100; index++)
        {
            Assert.InRange(
                policy.GetStartupDelay(),
                TimeSpan.Zero,
                TimeSpan.FromSeconds(30));
            Assert.InRange(
                policy.GetSuccessfulCycleDelay(),
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(35));
            Assert.InRange(
                policy.GetFailedCycleDelay(1),
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(35));
            Assert.InRange(
                policy.GetFailedCycleDelay(2),
                TimeSpan.FromMinutes(60),
                TimeSpan.FromMinutes(65));
            Assert.InRange(
                policy.GetFailedCycleDelay(3),
                TimeSpan.FromMinutes(120),
                TimeSpan.FromMinutes(125));
            Assert.InRange(
                policy.GetInlineRetryDelay(2, null)!.Value,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5));
            Assert.InRange(
                policy.GetInlineRetryDelay(3, null)!.Value,
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30));
        }
    }

    [Fact]
    public void RetryAfter_IsMinimumAndOverFiveMinutesStopsInlineRetry()
    {
        var policy = Policy();

        Assert.True(
            policy.GetInlineRetryDelay(2, TimeSpan.FromMinutes(4)) >=
            TimeSpan.FromMinutes(4));
        Assert.Null(policy.GetInlineRetryDelay(
            2,
            TimeSpan.FromMinutes(5) + TimeSpan.FromMilliseconds(1)));
    }

    private static SiteStateReconciliationPolicy Policy() =>
        new(Options.Create(new SiteStateReconciliationOptions
        {
            ReconciliationIntervalMinutes = 30
        }));
}
