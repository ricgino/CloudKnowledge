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
        return await (
            from membership in _dbContext.TeamMembers
            join team in _dbContext.Teams
                on membership.TeamId equals team.Id
            where membership.UserId == userId
            orderby team.Name, team.Id
            select new GetTeamsResult(
                team.Id,
                team.Name,
                membership.Role))
            .AsNoTracking()
            .ToListAsync(
                cancellationToken);
    }
}
