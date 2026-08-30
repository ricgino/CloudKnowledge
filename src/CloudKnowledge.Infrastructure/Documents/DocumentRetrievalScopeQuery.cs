using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Documents;

internal sealed class DocumentRetrievalScopeQuery
{
    private readonly CloudKnowledgeDbContext
        _dbContext;

    private readonly ITeamScopeResolver
        _teamScopeResolver;

    public DocumentRetrievalScopeQuery(
        CloudKnowledgeDbContext dbContext,
        ITeamScopeResolver teamScopeResolver)
    {
        _dbContext =
            dbContext;

        _teamScopeResolver =
            teamScopeResolver;
    }

    public async Task<IQueryable<Document>> CreateAsync(
        Guid userId,
        DocumentRetrievalScope scope,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User id cannot be empty.",
                nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(
            scope);

        IQueryable<Document> accessibleDocuments =
            _dbContext.Documents
                .AsNoTracking()
                .WhereAccessibleTo(
                    _dbContext,
                    userId);

        switch (scope.Kind)
        {
            case DocumentRetrievalScopeKind.All:
                return accessibleDocuments;

            case DocumentRetrievalScopeKind.Team:
                var allowedTeamIds =
                    await _teamScopeResolver.ResolveAllowedTeamIdsAsync(
                        userId,
                        scope.TeamId!.Value,
                        scope.IncludeDescendants,
                        cancellationToken);

                if (allowedTeamIds.Length == 0)
                {
                    return accessibleDocuments.Where(
                        _ => false);
                }

                return accessibleDocuments.Where(
                    document =>
                        (document.OwnerTeamId.HasValue
                         && allowedTeamIds.Contains(
                             document.OwnerTeamId.Value))

                        ||

                        _dbContext.DocumentTeamAccess.Any(
                            access =>
                                access.DocumentId == document.Id
                                && allowedTeamIds.Contains(
                                    access.TeamId)));

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scope),
                    scope.Kind,
                    "Unknown document retrieval scope.");
        }
    }
}
