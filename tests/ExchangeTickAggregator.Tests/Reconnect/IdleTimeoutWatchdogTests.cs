using ExchangeTickAggregator.Core.Reconnect;

namespace ExchangeTickAggregator.Tests.Reconnect;

public class IdleTimeoutWatchdogTests
{
    [Fact]
    public void HasTimedOut_becomes_true_when_activity_stops_longer_than_idle_timeout()
    {
        var watchdog = new IdleTimeoutWatchdog(TimeSpan.FromSeconds(5));
        var startedAt = DateTimeOffset.Parse("2023-07-22T10:00:00Z");

        watchdog.NotifyActivity(startedAt);

        Assert.False(watchdog.HasTimedOut(startedAt.AddSeconds(4)));
        Assert.True(watchdog.HasTimedOut(startedAt.AddSeconds(5)));
        Assert.True(watchdog.HasTimedOut(startedAt.AddSeconds(6)));

        watchdog.NotifyActivity(startedAt.AddSeconds(6));

        Assert.False(watchdog.HasTimedOut(startedAt.AddSeconds(10)));
        Assert.True(watchdog.HasTimedOut(startedAt.AddSeconds(11)));
    }
}
