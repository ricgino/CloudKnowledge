namespace CloudKnowledge.Application.Documents.AskDocuments;

public sealed record AnswerContextSource(
    string Label,
    Guid DocumentId,
    Guid ChunkId,
    int Position,
    string Content);