using CloudKnowledge.Application.Teams;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Infrastructure.Persistence;

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
}