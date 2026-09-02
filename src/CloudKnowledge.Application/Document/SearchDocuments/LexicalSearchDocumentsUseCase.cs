using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Documents.SearchDocuments;

public sealed class LexicalSearchDocumentsUseCase
{
    private readonly IDocumentLexicalSearchRepository
        _lexicalSearchRepository;

    private readonly ICurrentUser
        _currentUser;

    public LexicalSearchDocumentsUseCase(
        IDocumentLexicalSearchRepository lexicalSearchRepository,
        ICurrentUser currentUser)
    {
        _lexicalSearchRepository =
            lexicalSearchRepository;

        _currentUser =
            currentUser;
    }

    public async Task<IReadOnlyList<LexicalSearchResult>> ExecuteAsync(
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

        return await _lexicalSearchRepository
            .SearchAccessibleAsync(
                userId,
                query.Trim(),
                take,
                scope,
                cancellationToken);
    }
}
