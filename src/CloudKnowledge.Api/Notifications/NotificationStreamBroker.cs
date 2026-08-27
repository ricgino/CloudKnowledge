using System.Collections.Concurrent;
using System.Threading.Channels;
using CloudKnowledge.Application.Notifications;

namespace CloudKnowledge.Api.Notifications;

public sealed class NotificationStreamBroker
{
    private readonly ConcurrentDictionary<
        Guid,
        ConcurrentDictionary<Guid, Channel<NotificationResult>>>
        _subscriptions = new();

    public NotificationSubscription Subscribe(
        Guid userId)
    {
        var subscriptionId =
            Guid.NewGuid();

        var channel =
            Channel.CreateUnbounded<NotificationResult>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

        var userSubscriptions =
            _subscriptions.GetOrAdd(
                userId,
                _ => new ConcurrentDictionary<
                    Guid,
                    Channel<NotificationResult>>());

        userSubscriptions[subscriptionId] =
            channel;

        return new NotificationSubscription(
            channel.Reader,
            () =>
                Unsubscribe(
                    userId,
                    subscriptionId));
    }

    public void Publish(
        Guid userId,
        NotificationResult notification)
    {
        if (!_subscriptions.TryGetValue(
                userId,
                out var userSubscriptions))
        {
            return;
        }

        foreach (var channel in
                 userSubscriptions.Values)
        {
            channel.Writer.TryWrite(
                notification);
        }
    }

    private void Unsubscribe(
        Guid userId,
        Guid subscriptionId)
    {
        if (!_subscriptions.TryGetValue(
                userId,
                out var userSubscriptions))
        {
            return;
        }

        if (userSubscriptions.TryRemove(
                subscriptionId,
                out var channel))
        {
            channel.Writer.TryComplete();
        }

        if (userSubscriptions.IsEmpty)
        {
            _subscriptions.TryRemove(
                userId,
                out _);
        }
    }
}

public sealed class NotificationSubscription
    : IAsyncDisposable
{
    private readonly Action _dispose;
    private int _disposed;

    public ChannelReader<NotificationResult> Reader
    {
        get;
    }

    public NotificationSubscription(
        ChannelReader<NotificationResult> reader,
        Action dispose)
    {
        Reader = reader;
        _dispose = dispose;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1) == 0)
        {
            _dispose();
        }

        return ValueTask.CompletedTask;
    }
}
