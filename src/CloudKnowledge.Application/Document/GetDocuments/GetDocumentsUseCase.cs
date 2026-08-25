using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Documents.GetDocuments;

public sealed class GetDocumentsUseCase
{
    private const int MaxPageSize =
        100;

    private readonly IDocumentAccessRepository
        _documentAccessRepository;

    private readonly ICurrentUser
        _currentUser;

    public GetDocumentsUseCase(
        IDocumentAccessRepository documentAccessRepository,
        ICurrentUser currentUser)
    {
        _documentAccessRepository =
            documentAccessRepository;

        _currentUser =
            currentUser;
    }

    public async Task<GetDocumentsResult> ExecuteAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                "Page must be greater than zero.");
        }

        if (pageSize < 1 ||
            pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                $"Page size must be between 1 and {MaxPageSize}.");
        }

        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var skip =
            (page - 1) * pageSize;

        var totalCount =
            await _documentAccessRepository
                .CountAsync(
                    userId,
                    cancellationToken);

        var documents =
            await _documentAccessRepository
                .GetPageAsync(
                    userId,
                    skip,
                    pageSize,
                    cancellationToken);

        var items =
            documents
                .Select(
                    document =>
                        new GetDocumentsItem(
                            document.Id,
                            document.FileName,
                            document.ContentType,
                            document.Status))
                .ToList();

        var totalPages =
            (int)Math.Ceiling(
                totalCount /
                (double)pageSize);

        return new GetDocumentsResult(
            items,
            page,
            pageSize,
            totalCount,
            totalPages);
    }
}