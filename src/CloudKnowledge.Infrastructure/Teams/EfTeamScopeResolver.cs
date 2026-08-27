using CloudKnowledge.Application.Teams;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Teams;

public sealed class EfTeamScopeResolver
    : ITeamScopeResolver
{
    private readonly CloudKnowledgeDbContext
        _dbContext;

    public EfTeamScopeResolver(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task<Guid[]> ResolveAllowedTeamIdsAsync(
        Guid userId,
        Guid selectedTeamId,
        bool includeDescendants,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User id cannot be empty.",
                nameof(userId));
        }

        if (selectedTeamId == Guid.Empty)
        {
            throw new ArgumentException(
                "Selected team id cannot be empty.",
                nameof(selectedTeamId));
        }

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
                        new TeamHierarchyNode(
                            team.Id,
                            team.ParentTeamId))
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

        var pendingParents =
            new Queue<Guid>();

        pendingParents.Enqueue(
            selectedTeamId);

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

    private sealed record TeamHierarchyNode(
        Guid Id,
        Guid? ParentTeamId);
}
