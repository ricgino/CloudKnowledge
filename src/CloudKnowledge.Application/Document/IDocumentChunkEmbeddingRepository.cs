namespace CloudKnowledge.Application.Documents;

public interface IDocumentChunkEmbeddingRepository
{
    Task ReplaceForDocumentAsync(
        Guid documentId,
        IReadOnlyCollection<DocumentChunkEmbedding> embeddings,
        CancellationToken cancellationToken);
}