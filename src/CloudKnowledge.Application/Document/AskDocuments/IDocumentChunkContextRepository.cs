namespace CloudKnowledge.Application.Documents.AskDocuments;

public interface IDocumentChunkContextRepository
{
    Task<DocumentChunkContextResult?> GetAccessibleNextAsync(
        Guid userId,
        Guid documentId,
        int position,
        DocumentRetrievalScope scope,
        CancellationToken cancellationToken);
}

public sealed record DocumentChunkContextResult(
    Guid DocumentId,
    Guid ChunkId,
    int Position,
    string Content);
