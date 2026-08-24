using CloudKnowledge.Application.Documents;

namespace CloudKnowledge.Application.Documents.SearchDocuments;

public sealed class SearchDocumentsUseCase
{
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IDocumentSemanticSearchRepository
        _semanticSearchRepository;

    public SearchDocumentsUseCase(
        IEmbeddingGenerator embeddingGenerator,
        IDocumentSemanticSearchRepository semanticSearchRepository)
    {
        _embeddingGenerator =
            embeddingGenerator;

        _semanticSearchRepository =
            semanticSearchRepository;
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> ExecuteAsync(
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException(
                "Search query cannot be empty.",
                nameof(query));
        }

        if (take < 1 || take > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                "Take must be between 1 and 20.");
        }

        var vectors =
            await _embeddingGenerator.GenerateAsync(
                new[]
                {
                    query
                },
                cancellationToken);

        if (vectors.Count != 1)
        {
            throw new InvalidOperationException(
                "The embedding generator returned " +
                "an unexpected number of embeddings.");
        }

        var queryEmbedding =
            vectors[0];

        if (queryEmbedding.Length !=
            _embeddingGenerator.Dimensions)
        {
            throw new InvalidOperationException(
                "The embedding generator returned " +
                "an embedding with an invalid dimension.");
        }

        return await _semanticSearchRepository.SearchAsync(
            queryEmbedding,
            take,
            cancellationToken);
    }
}