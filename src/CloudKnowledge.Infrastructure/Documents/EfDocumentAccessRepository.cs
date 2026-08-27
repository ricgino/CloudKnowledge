using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Application.Documents.GetDocuments;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentAccessRepository
    : IDocumentAccessRepository
{
    private readonly CloudKnowledgeDbContext _dbContext;

    public EfDocumentAccessRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> CanAccessAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Documents
            .AsNoTracking()
            .WhereAccessibleTo(
                _dbContext,
                userId)
            .AnyAsync(
                document => document.Id == documentId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentAccessResult>> GetPageAsync(
        Guid userId,
        int skip,
        int take,
        GetDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var documents =
            ApplyFilters(
                    userId,
                    query)
                .OrderByDescending(
                    document => document.CreatedAtUtc)
                .ThenByDescending(
                    document => document.Id)
                .Skip(skip)
                .Take(take);

        return await documents
            .Select(document =>
                new DocumentAccessResult(
                    document.Id,
                    document.FileName,
                    document.ContentType,
                    document.Status,
                    document.OwnerUserId == userId,
                    Array.Empty<DocumentAccessTeamResult>()))
            .ToListAsync(
                cancellationToken);
    }

    public Task<int> CountAsync(
        Guid userId,
        GetDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        return ApplyFilters(
                userId,
                query)
            .CountAsync(
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

        foreach (var group in
                 visibleTeamAccess
                     .GroupBy(
                         access => access.DocumentId))
        {
            var accessTeams =
                group
                    .Select(access =>
                    {
                        if (!teamsById.TryGetValue(
                                access.TeamId,
                                out var team))
                        {
                            return null;
                        }

                        return new DocumentAccessTeamResult(
                            team.Id,
                            team.Name,
                            BuildTeamPath(
                                team,
                                teamsById));
                    })
                    .Where(team => team is not null)
                    .Select(team => team!)
                    .OrderBy(team => team.Path)
                    .ThenBy(team => team.Name)
                    .ToArray();

            result[group.Key] =
                accessTeams;
        }

        return result;
    }

    private IQueryable<CloudKnowledge.Domain.Documents.Document> ApplyFilters(
        Guid userId,
        GetDocumentsQuery query)
    {
        IQueryable<CloudKnowledge.Domain.Documents.Document> documents =
            _dbContext.Documents
                .AsNoTracking()
                .WhereAccessibleTo(
                    _dbContext,
                    userId);

        if (!string.IsNullOrWhiteSpace(
                query.SearchQuery))
        {
            var search =
                query.SearchQuery.Trim();

            documents =
                documents.Where(
                    document =>
                        EF.Functions.ILike(
                            document.FileName,
                            $"%{search}%"));
        }

        if (query.Scope == DocumentListScope.Owned)
        {
            documents =
                documents.Where(
                    document =>
                        document.OwnerUserId == userId);
        }
        else if (query.Scope == DocumentListScope.Team)
        {
            var teamId =
                query.TeamId!.Value;

            var selectedTeamIds =
                GetSelectedTeamIds(
                    teamId,
                    query.IncludeDescendants);

            var authorizedTeamIds =
                _dbContext.TeamMembers
                    .AsNoTracking()
                    .Where(
                        member =>
                            member.UserId == userId &&
                            selectedTeamIds.Contains(
                                member.TeamId))
                    .Select(
                        member => member.TeamId);

            documents =
                documents.Where(
                    document =>
                        document.OwnerTeamId.HasValue &&
                        authorizedTeamIds.Contains(
                            document.OwnerTeamId.Value) ||
                        _dbContext.DocumentTeamAccess.Any(
                            access =>
                                access.DocumentId == document.Id &&
                                authorizedTeamIds.Contains(
                                    access.TeamId)));
        }

        return documents;
    }

    private IQueryable<Guid> GetSelectedTeamIds(
        Guid teamId,
        bool includeDescendants)
    {
        if (!includeDescendants)
        {
            return _dbContext.Teams
                .AsNoTracking()
                .Where(team => team.Id == teamId)
                .Select(team => team.Id);
        }

        var descendants =
            _dbContext.Teams
                .FromSqlInterpolated(
                    $"""
                    WITH RECURSIVE team_tree AS (
                        SELECT id, parent_team_id
                        FROM teams
                        WHERE id = {teamId}

                        UNION ALL

                        SELECT child.id, child.parent_team_id
                        FROM teams child
                        INNER JOIN team_tree parent
                            ON child.parent_team_id = parent.id
                    )
                    SELECT id, name, parent_team_id, created_at_utc
                    FROM teams
                    WHERE id IN (SELECT id FROM team_tree)
                    """)
                .AsNoTracking()
                .Select(team => team.Id);

        return descendants;
    }

    private static string BuildTeamPath(
        CloudKnowledge.Domain.Teams.Team team,
        IReadOnlyDictionary<Guid, CloudKnowledge.Domain.Teams.Team> teamsById)
    {
        var names =
            new List<string>();

        var visited =
            new HashSet<Guid>();

        var current = team;

        while (visited.Add(current.Id))
        {
            names.Add(current.Name);

            if (!current.ParentTeamId.HasValue ||
                !teamsById.TryGetValue(
                    current.ParentTeamId.Value,
                    out var parent))
            {
                break;
            }

            current = parent;
        }

        names.Reverse();

        return string.Join(
            " / ",
            names);
    }
}
