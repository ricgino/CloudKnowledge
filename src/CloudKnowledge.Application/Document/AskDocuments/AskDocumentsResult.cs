namespace CloudKnowledge.Application.Documents.AskDocuments;

public sealed record AskDocumentsResult(
    string Answer,
    IReadOnlyList<AskDocumentsSource> Sources);