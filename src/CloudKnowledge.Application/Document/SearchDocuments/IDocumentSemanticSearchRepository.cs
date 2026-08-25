namespace CloudKnowledge.Application.Documents.SearchDocuments;

public interface IDocumentSemanticSearchRepository
{
    Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SemanticSearchResult>> SearchAccessibleAsync(
        Guid userId,
        float[] queryEmbedding,
        int take,
        CancellationToken cancellationToken);
}