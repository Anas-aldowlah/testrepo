using System.Threading.Channels;

namespace YAGOT_2._0.Services;

public sealed record VisitQueueItem(
    string Ip,
    string UserAgent,
    string VisitorName,
    DateTime TimestampUtc);

public interface IVisitBackgroundQueue
{
    void QueueVisit(VisitQueueItem item);
    IAsyncEnumerable<VisitQueueItem> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class VisitBackgroundQueue : IVisitBackgroundQueue
{
    private readonly Channel<VisitQueueItem> _channel;

    public VisitBackgroundQueue()
    {
        var options = new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<VisitQueueItem>(options);
    }

    public void QueueVisit(VisitQueueItem item)
    {
        _channel.Writer.TryWrite(item);
    }

    public IAsyncEnumerable<VisitQueueItem> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
