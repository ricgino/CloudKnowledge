namespace CloudKnowledge.Application.Documents;

public sealed record DocumentProcessingMessage(
    Guid DocumentId);