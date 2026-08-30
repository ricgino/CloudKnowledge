namespace CloudKnowledge.Application.Documents.SearchDocuments;

public sealed record LexicalSearchResult(
    Guid DocumentId,
    Guid ChunkId,
    int Position,
    string Content,
    double Rank);
