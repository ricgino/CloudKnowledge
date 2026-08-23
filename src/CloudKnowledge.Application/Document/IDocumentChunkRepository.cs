using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents;

public interface IDocumentChunkRepository
{
    Task ReplaceForDocumentAsync(
        Guid documentId,
        IReadOnlyCollection<DocumentChunk> chunks,
        CancellationToken cancellationToken);
}