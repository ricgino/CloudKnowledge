namespace CloudKnowledge.Application.Documents.SearchDocuments;

public interface IDocumentLexicalSearchRepository
{
    Task<IReadOnlyList<LexicalSearchResult>> SearchAccessibleAsync(
        Guid userId,
        string query,
        int take,
        DocumentRetrievalScope scope,
        CancellationToken cancellationToken);
}
