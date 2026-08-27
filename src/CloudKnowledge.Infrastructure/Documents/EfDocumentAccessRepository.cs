using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Application.Documents.GetDocuments;
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

    public EfDocumentAccessRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext =
            dbContext;
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

        if (visibleShares.Count == 0)
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

        foreach (var group in visibleShares.GroupBy(
                     share => share.DocumentId))
        {
            var sharedTeams =
                group
                    .Where(
                        share =>
                            teamsById.ContainsKey(
                                share.TeamId))
                    .Select(
                        share =>
                        {
                            var team =
                                teamsById[share.TeamId];

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
                    await ResolveAllowedTeamIdsAsync(
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

    private async Task<Guid[]> ResolveAllowedTeamIdsAsync(
        Guid userId,
        Guid selectedTeamId,
        bool includeDescendants,
        CancellationToken cancellationToken)
    {
        if (!includeDescendants)
        {
            var isDirectMember =
                await _dbContext.TeamMembers
                    .AsNoTracking()
                    .AnyAsync(
                        membership =>
                            membership.UserId == userId
                            && membership.TeamId == selectedTeamId,
                        cancellationToken);

            return isDirectMember
                ? new[] { selectedTeamId }
                : Array.Empty<Guid>();
        }

        var teams =
            await _dbContext.Teams
                .AsNoTracking()
                .Select(
                    team =>
                        new
                        {
                            team.Id,
                            team.ParentTeamId
                        })
                .ToListAsync(
                    cancellationToken);

        if (!teams.Any(
                team => team.Id == selectedTeamId))
        {
            return Array.Empty<Guid>();
        }

        var branchTeamIds =
            new HashSet<Guid>
            {
                selectedTeamId
            };

        var pendingParents =
            new Queue<Guid>();

        pendingParents.Enqueue(
            selectedTeamId);

        var childrenByParent =
            teams
                .Where(
                    team => team.ParentTeamId.HasValue)
                .GroupBy(
                    team => team.ParentTeamId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(team => team.Id)
                        .ToArray());

        while (pendingParents.Count > 0)
        {
            var parentId =
                pendingParents.Dequeue();

            if (!childrenByParent.TryGetValue(
                    parentId,
                    out var childIds))
            {
                continue;
            }

            foreach (var childId in childIds)
            {
                if (branchTeamIds.Add(
                        childId))
                {
                    pendingParents.Enqueue(
                        childId);
                }
            }
        }

        return await _dbContext.TeamMembers
            .AsNoTracking()
            .Where(
                membership =>
                    membership.UserId == userId
                    && branchTeamIds.Contains(
                        membership.TeamId))
            .Select(
                membership => membership.TeamId)
            .Distinct()
            .ToArrayAsync(
                cancellationToken);
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
