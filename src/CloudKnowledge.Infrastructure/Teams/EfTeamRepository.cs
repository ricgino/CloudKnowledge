using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Teams.GetTeams;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Teams;

public sealed class EfTeamRepository
    : ITeamRepository
{
    private readonly CloudKnowledgeDbContext
        _dbContext;

    public EfTeamRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task<Team?> GetByIdAsync(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Teams
            .AsNoTracking()
            .SingleOrDefaultAsync(
                team => team.Id == teamId,
                cancellationToken);
    }

    public async Task AddAsync(
        Team team,
        TeamMember ownerMembership,
        CancellationToken cancellationToken)
    {
        _dbContext.Teams.Add(
            team);

        _dbContext.TeamMembers.Add(
            ownerMembership);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<IReadOnlyList<GetTeamsResult>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var teams =
            await _dbContext.Teams
                .AsNoTracking()
                .ToListAsync(
                    cancellationToken);

        var memberships =
            await _dbContext.TeamMembers
                .AsNoTracking()
                .Where(
                    membership =>
                        membership.UserId == userId)
                .ToListAsync(
                    cancellationToken);

        if (memberships.Count == 0)
        {
            return Array.Empty<GetTeamsResult>();
        }

        var teamsById =
            teams.ToDictionary(
                team => team.Id);

        var membershipsByTeamId =
            memberships.ToDictionary(
                membership => membership.TeamId);

        var visibleTeamIds =
            new HashSet<Guid>(
                membershipsByTeamId.Keys);

        foreach (var membership in memberships)
        {
            if (!teamsById.TryGetValue(
                    membership.TeamId,
                    out var currentTeam))
            {
                continue;
            }

            var visitedAncestors =
                new HashSet<Guid>();

            while (currentTeam.ParentTeamId is Guid parentTeamId &&
                   teamsById.TryGetValue(
                       parentTeamId,
                       out var parentTeam))
            {
                if (!visitedAncestors.Add(
                        parentTeamId))
                {
                    break;
                }

                visibleTeamIds.Add(
                    parentTeamId);

                currentTeam =
                    parentTeam;
            }
        }

        return visibleTeamIds
            .Select(
                teamId =>
                {
                    var team =
                        teamsById[teamId];

                    var isMember =
                        membershipsByTeamId.TryGetValue(
                            teamId,
                            out var membership);

                    TeamRole? role =
                        isMember
                            ? membership!.Role
                            : null;

                    var canManage =
                        role is TeamRole.Admin or TeamRole.Owner;

                    return new GetTeamsResult(
                        team.Id,
                        team.Name,
                        team.ParentTeamId,
                        isMember,
                        role,
                        canManage);
                })
            .OrderBy(
                result => result.Name)
            .ThenBy(
                result => result.Id)
            .ToList();
    }
}
