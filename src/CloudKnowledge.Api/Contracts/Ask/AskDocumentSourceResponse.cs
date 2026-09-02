namespace CloudKnowledge.Api.Contracts.Ask;

public sealed record AskDocumentSourceResponse(
    string Label,
    Guid DocumentId,
    Guid ChunkId,
    int Position,
    string Content,
    double? Similarity);
