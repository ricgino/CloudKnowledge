using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Documents.SearchDocuments;

public sealed class SearchDocumentsUseCase
{
    private readonly IEmbeddingGenerator
        _embeddingGenerator;

    private readonly IDocumentSemanticSearchRepository
        _semanticSearchRepository;

    private readonly ICurrentUser
        _currentUser;

    public SearchDocumentsUseCase(
        IEmbeddingGenerator embeddingGenerator,
        IDocumentSemanticSearchRepository semanticSearchRepository,
        ICurrentUser currentUser)
    {
        _embeddingGenerator =
            embeddingGenerator;

        _semanticSearchRepository =
            semanticSearchRepository;

        _currentUser =
            currentUser;
    }

    public Task<IReadOnlyList<SemanticSearchResult>> ExecuteAsync(
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            query,
            take,
            DocumentRetrievalScope.All,
            cancellationToken);
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> ExecuteAsync(
        string query,
        int take,
        DocumentRetrievalScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            scope);

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

        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

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

        return await _semanticSearchRepository
            .SearchAccessibleAsync(
                userId,
                queryEmbedding,
                take,
                scope,
                cancellationToken);
    }
}
