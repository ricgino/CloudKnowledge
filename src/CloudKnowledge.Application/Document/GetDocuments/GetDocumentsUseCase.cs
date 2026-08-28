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

    public Task<GetDocumentsResult> ExecuteAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            new GetDocumentsQuery(
                page,
                pageSize,
                DocumentListScope.All,
                null,
                false,
                null),
            cancellationToken);
    }

    public async Task<GetDocumentsResult> ExecuteAsync(
        GetDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        Validate(
            query);

        var normalizedQuery =
            query with
            {
                SearchQuery =
                    string.IsNullOrWhiteSpace(
                        query.SearchQuery)
                        ? null
                        : query.SearchQuery.Trim()
            };

        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var skip =
            (normalizedQuery.Page - 1) *
            normalizedQuery.PageSize;

        var totalCount =
            await _documentAccessRepository
                .CountAsync(
                    userId,
                    normalizedQuery,
                    cancellationToken);

        var documents =
            await _documentAccessRepository
                .GetPageAsync(
                    userId,
                    skip,
                    normalizedQuery.PageSize,
                    normalizedQuery,
                    cancellationToken);

        var documentIds =
            documents
                .Select(document => document.Id)
                .ToArray();

        var visibleTeamAccess =
            await _documentAccessRepository
                .GetVisibleTeamAccessAsync(
                    userId,
                    documentIds,
                    cancellationToken);

        var teamOwnedDeletableDocumentIds =
            await _documentAccessRepository
                .GetTeamOwnedDeletableDocumentIdsAsync(
                    userId,
                    documentIds,
                    cancellationToken);

        var teamOwnedDeletableDocumentIdSet =
            teamOwnedDeletableDocumentIds.ToHashSet();

        var items =
            documents
                .Select(
                    document =>
                    {
                        visibleTeamAccess.TryGetValue(
                            document.Id,
                            out var sharedTeams);

                        var isOwner =
                            document.OwnerUserId == userId;

                        return new GetDocumentsItem(
                            document.Id,
                            document.FileName,
                            document.ContentType,
                            document.Status,
                            isOwner,
                            isOwner ||
                            teamOwnedDeletableDocumentIdSet.Contains(
                                document.Id),
                            sharedTeams ??
                                Array.Empty<DocumentAccessTeamResult>());
                    })
                .ToList();

        var totalPages =
            (int)Math.Ceiling(
                totalCount /
                (double)normalizedQuery.PageSize);

        return new GetDocumentsResult(
            items,
            normalizedQuery.Page,
            normalizedQuery.PageSize,
            totalCount,
            totalPages);
    }

    private static void Validate(
        GetDocumentsQuery query)
    {
        if (query.Page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query.Page),
                "Page must be greater than zero.");
        }

        if (query.PageSize < 1 ||
            query.PageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query.PageSize),
                $"Page size must be between 1 and {MaxPageSize}.");
        }

        if (query.Scope == DocumentListScope.Team)
        {
            if (!query.TeamId.HasValue ||
                query.TeamId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Team scope requires a valid team id.",
                    nameof(query.TeamId));
            }

            return;
        }

        if (query.TeamId.HasValue)
        {
            throw new ArgumentException(
                "Team id is valid only for team scope.",
                nameof(query.TeamId));
        }

        if (query.IncludeDescendants)
        {
            throw new ArgumentException(
                "Descendant aggregation is valid only for team scope.",
                nameof(query.IncludeDescendants));
        }
    }
}
