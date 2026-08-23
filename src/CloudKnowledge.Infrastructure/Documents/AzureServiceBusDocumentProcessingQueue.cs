using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CloudKnowledge.Application.Documents;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class AzureServiceBusDocumentProcessingQueue
    : IDocumentProcessingQueue
{
    private readonly ServiceBusSender _sender;

    public AzureServiceBusDocumentProcessingQueue(
        ServiceBusSender sender)
    {
        _sender = sender;
    }

    public async Task PublishAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var payload = new DocumentProcessingMessage(
            documentId);

        var json =
            JsonSerializer.Serialize(payload);

        var message =
            new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                MessageId = documentId.ToString()
            };

        await _sender.SendMessageAsync(
            message,
            cancellationToken);
    }

}