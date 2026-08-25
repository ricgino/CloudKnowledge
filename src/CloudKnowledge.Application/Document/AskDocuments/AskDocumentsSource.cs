namespace CloudKnowledge.Application.Documents.AskDocuments;

public sealed record AskDocumentsSource(
    string Label,
    Guid DocumentId,
    Guid ChunkId,
    int Position,
    string Content,
    double Similarity);