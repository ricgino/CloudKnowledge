namespace CloudKnowledge.Application.Documents.SearchDocuments;

public sealed record SemanticSearchResult(
    Guid DocumentId,
    Guid ChunkId,
    int Position,
    string Content,
    double CosineDistance);