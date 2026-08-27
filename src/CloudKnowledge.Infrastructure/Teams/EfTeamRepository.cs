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
        return await (
            from team in _dbContext.Teams.AsNoTracking()
            join membership in _dbContext.TeamMembers.AsNoTracking()
                on team.Id equals membership.TeamId
            where membership.UserId == userId
            orderby team.Name, team.Id
            select new GetTeamsResult(
                team.Id,
                team.Name,
                membership.Role))
            .ToListAsync(
                cancellationToken);
    }
}
