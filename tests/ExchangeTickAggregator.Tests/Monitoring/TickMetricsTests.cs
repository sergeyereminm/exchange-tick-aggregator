using ExchangeTickAggregator.Core.Monitoring;

namespace ExchangeTickAggregator.Tests.Monitoring;

public class TickMetricsTests
{
    [Fact]
    public void GetSnapshot_returns_recorded_tick_outcomes()
    {
        var metrics = new TickMetrics();

        metrics.RecordReceived();
        metrics.RecordReceived();
        metrics.RecordMalformed();
        metrics.RecordDuplicate();
        metrics.RecordBuffered();
        metrics.RecordPersisted(3);
        metrics.RecordDropped(2);

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(2, snapshot.ReceivedTickCount);
        Assert.Equal(1, snapshot.MalformedTickCount);
        Assert.Equal(1, snapshot.DuplicateTickCount);
        Assert.Equal(1, snapshot.BufferedTickCount);
        Assert.Equal(3, snapshot.PersistedTickCount);
        Assert.Equal(2, snapshot.DroppedTickCount);
    }
}
