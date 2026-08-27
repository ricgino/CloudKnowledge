using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CloudKnowledge.Application.Notifications.DocumentReady;

namespace CloudKnowledge.Infrastructure.Notifications;

public sealed class AzureServiceBusDocumentReadyPublisher
    : IDocumentReadyPublisher,
      IAsyncDisposable
{
    private readonly ServiceBusSender _sender;

    public AzureServiceBusDocumentReadyPublisher(
        ServiceBusSender sender)
    {
        _sender = sender;
    }

    public async Task PublishAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var payload =
            new DocumentReadyMessage(
                documentId);

        var message =
            new ServiceBusMessage(
                JsonSerializer.Serialize(payload))
            {
                ContentType = "application/json",
                MessageId = $"document-ready:{documentId:D}"
            };

        await _sender.SendMessageAsync(
            message,
            cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _sender.DisposeAsync();
    }
}
