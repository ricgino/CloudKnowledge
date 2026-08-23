namespace CloudKnowledge.Application.Documents;

public interface IDocumentProcessingQueue
{
    Task PublishAsync(
        Guid documentId,
        CancellationToken cancellationToken);
}