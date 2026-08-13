using ExchangeTickAggregator.Core.Reconnect;

namespace ExchangeTickAggregator.Tests.Reconnect;

public class ExponentialBackoffPolicyTests
{
    [Fact]
    public void NextDelay_grows_exponentially_until_cap_across_repeated_attempts()
    {
        var policy = new ExponentialBackoffPolicy(
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromSeconds(8),
            factor: 2.0);

        Assert.Equal(TimeSpan.FromSeconds(1), policy.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(2), policy.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(4), policy.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(8), policy.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(8), policy.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(8), policy.NextDelay());
    }
}
