namespace CloudKnowledge.Api.Contracts.Search;

public sealed record SearchDocumentResultResponse(
    Guid DocumentId,
    Guid ChunkId,
    int Position,
    string Content,
    double Similarity);