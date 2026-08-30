namespace CloudKnowledge.Application.Documents.AskDocuments;

public sealed record AskDocumentsResult(
    string Answer,
    IReadOnlyList<AskDocumentsSource> Sources,
    IReadOnlyList<string> RetrievalQueries,
    IReadOnlyList<AskRetrievalQueryDiagnostics> RetrievalDiagnostics)
{
    public AskDocumentsResult(
        string answer,
        IReadOnlyList<AskDocumentsSource> sources,
        IReadOnlyList<string> retrievalQueries)
        : this(
            answer,
            sources,
            retrievalQueries,
            Array.Empty<AskRetrievalQueryDiagnostics>())
    {
    }
}
