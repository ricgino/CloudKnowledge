using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Application.Documents.GetDocuments;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentAccessRepository
    : IDocumentAccessRepository
{
    private readonly CloudKnowledgeDbContext
        _dbContext;

    private readonly ITeamScopeResolver
        _teamScopeResolver;

    public EfDocumentAccessRepository(
        CloudKnowledgeDbContext dbContext,
        ITeamScopeResolver teamScopeResolver)
    {
        _dbContext =
            dbContext;

        _teamScopeResolver =
            teamScopeResolver;
    }

    public async Task<bool> CanAccessAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document id cannot be empty.",
                nameof(documentId));
        }

        return await _dbContext.Documents
            .AsNoTracking()
            .WhereAccessibleTo(
                _dbContext,
                userId)
            .AnyAsync(
                document =>
                    document.Id == documentId,
                cancellationToken);
    }

    public async Task<Document?> GetByIdAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document id cannot be empty.",
                nameof(documentId));
        }

        return await _dbContext.Documents
            .AsNoTracking()
            .WhereAccessibleTo(
                _dbContext,
                userId)
            .SingleOrDefaultAsync(
                document =>
                    document.Id == documentId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetPageAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .WhereAccessibleTo(
                _dbContext,
                userId)
            .OrderByDescending(
                document =>
                    document.CreatedAtUtc)
            .ThenBy(
                document =>
                    document.Id)
            .Skip(
                skip)
            .Take(
                take)
            .ToListAsync(
                cancellationToken);
    }

    public async Task<int> CountAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .WhereAccessibleTo(
                _dbContext,
                userId)
            .CountAsync(
                cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetPageAsync(
        Guid userId,
        int skip,
        int take,
        GetDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var documents =
            await BuildFilteredQueryAsync(
                userId,
                query,
                cancellationToken);

        return await documents
            .OrderByDescending(
                document =>
                    document.CreatedAtUtc)
            .ThenBy(
                document =>
                    document.Id)
            .Skip(
                skip)
            .Take(
                take)
            .ToListAsync(
                cancellationToken);
    }

    public async Task<int> CountAsync(
        Guid userId,
        GetDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var documents =
            await BuildFilteredQueryAsync(
                userId,
                query,
                cancellationToken);

        return await documents.CountAsync(
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<DocumentAccessTeamResult>>>
        GetVisibleTeamAccessAsync(
            Guid userId,
            IReadOnlyCollection<Guid> documentIds,
            CancellationToken cancellationToken)
    {
        var distinctDocumentIds =
            documentIds
                .Where(documentId => documentId != Guid.Empty)
                .Distinct()
                .ToArray();

        var result =
            distinctDocumentIds.ToDictionary(
                documentId => documentId,
                _ =>
                    (IReadOnlyList<DocumentAccessTeamResult>)
                    Array.Empty<DocumentAccessTeamResult>());

        if (distinctDocumentIds.Length == 0)
        {
            return result;
        }

        var visibleShares =
            await (
                from access in _dbContext.DocumentTeamAccess.AsNoTracking()
                join membership in _dbContext.TeamMembers.AsNoTracking()
                    on access.TeamId equals membership.TeamId
                where distinctDocumentIds.Contains(access.DocumentId)
                      && membership.UserId == userId
                select new
                {
                    access.DocumentId,
                    access.TeamId
                })
                .Distinct()
                .ToListAsync(
                    cancellationToken);

        var visibleOwnership =
            await (
                from document in _dbContext.Documents.AsNoTracking()
                join membership in _dbContext.TeamMembers.AsNoTracking()
                    on document.OwnerTeamId equals membership.TeamId
                where distinctDocumentIds.Contains(document.Id)
                      && document.OwnerTeamId.HasValue
                      && membership.UserId == userId
                select new
                {
                    DocumentId = document.Id,
                    TeamId = membership.TeamId
                })
                .Distinct()
                .ToListAsync(
                    cancellationToken);

        var visibleTeamAccess =
            visibleShares
                .Concat(visibleOwnership)
                .DistinctBy(
                    access =>
                        (access.DocumentId, access.TeamId))
                .ToArray();

        if (visibleTeamAccess.Length == 0)
        {
            return result;
        }

        var teams =
            await _dbContext.Teams
                .AsNoTracking()
                .ToListAsync(
                    cancellationToken);

        var teamsById =
            teams.ToDictionary(
                team => team.Id);

        foreach (var group in visibleTeamAccess.GroupBy(
                     access => access.DocumentId))
        {
            var sharedTeams =
                group
                    .Where(
                        access =>
                            teamsById.ContainsKey(
                                access.TeamId))
                    .Select(
                        access =>
                        {
                            var team =
                                teamsById[access.TeamId];

                            return new DocumentAccessTeamResult(
                                team.Id,
                                team.Name,
                                BuildPath(
                                    team,
                                    teamsById));
                        })
                    .OrderBy(
                        team => team.Path)
                    .ThenBy(
                        team => team.Id)
                    .ToArray();

            result[group.Key] =
                sharedTeams;
        }

        return result;
    }

    public async Task<IReadOnlyCollection<Guid>> GetTeamOwnedDeletableDocumentIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken)
    {
        var distinctDocumentIds =
            documentIds
                .Where(documentId => documentId != Guid.Empty)
                .Distinct()
                .ToArray();

        if (distinctDocumentIds.Length == 0)
        {
            return Array.Empty<Guid>();
        }

        return await (
                from document in _dbContext.Documents.AsNoTracking()
                join membership in _dbContext.TeamMembers.AsNoTracking()
                    on document.OwnerTeamId equals membership.TeamId
                where distinctDocumentIds.Contains(document.Id)
                      && document.OwnerTeamId.HasValue
                      && membership.UserId == userId
                      && membership.Role == TeamRole.Owner
                select document.Id)
            .Distinct()
            .ToArrayAsync(
                cancellationToken);
    }

    private async Task<IQueryable<Document>> BuildFilteredQueryAsync(
        Guid userId,
        GetDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<Document> documents =
            _dbContext.Documents
                .AsNoTracking();

        switch (query.Scope)
        {
            case DocumentListScope.All:
                documents =
                    documents.WhereAccessibleTo(
                        _dbContext,
                        userId);
                break;

            case DocumentListScope.Owned:
                documents =
                    documents.Where(
                        document =>
                            document.OwnerUserId == userId);
                break;

            case DocumentListScope.Team:
                var allowedTeamIds =
                    await _teamScopeResolver.ResolveAllowedTeamIdsAsync(
                        userId,
                        query.TeamId!.Value,
                        query.IncludeDescendants,
                        cancellationToken);

                if (allowedTeamIds.Length == 0)
                {
                    documents =
                        documents.Where(
                            _ => false);
                    break;
                }

                documents =
                    documents.Where(
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
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(query.Scope),
                    query.Scope,
                    "Unknown document list scope.");
        }

        if (!string.IsNullOrWhiteSpace(
                query.SearchQuery))
        {
            var pattern =
                $"%{query.SearchQuery.Trim()}%";

            documents =
                documents.Where(
                    document =>
                        EF.Functions.ILike(
                            document.FileName,
                            pattern));
        }

        return documents;
    }

    private static string BuildPath(
        Team team,
        IReadOnlyDictionary<Guid, Team> teamsById)
    {
        var names =
            new List<string>();

        var visited =
            new HashSet<Guid>();

        var current =
            team;

        while (visited.Add(
                   current.Id))
        {
            names.Add(
                current.Name);

            if (current.ParentTeamId is not Guid parentTeamId ||
                !teamsById.TryGetValue(
                    parentTeamId,
                    out var parent))
            {
                break;
            }

            current =
                parent;
        }

        names.Reverse();

        return string.Join(
            " / ",
            names);
    }
}
