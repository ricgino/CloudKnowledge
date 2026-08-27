namespace CloudKnowledge.Application.Notifications.DocumentReady;

public interface IDocumentReadyPublisher
{
    Task PublishAsync(
        Guid documentId,
        CancellationToken cancellationToken);
}
