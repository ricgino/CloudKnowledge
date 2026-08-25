using CloudKnowledge.Application.Teams;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Teams;

public sealed class EfTeamMembershipRepository
    : ITeamMembershipRepository
{
    private readonly CloudKnowledgeDbContext
        _dbContext;

    public EfTeamMembershipRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task<TeamMember?> GetMembershipAsync(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.TeamMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                member =>
                    member.TeamId == teamId &&
                    member.UserId == userId,
                cancellationToken);
    }

    public async Task<bool> IsMemberAsync(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.TeamMembers
            .AsNoTracking()
            .AnyAsync(
                member =>
                    member.TeamId == teamId &&
                    member.UserId == userId,
                cancellationToken);
    }

    public async Task AddAsync(
        TeamMember membership,
        CancellationToken cancellationToken)
    {
        _dbContext.TeamMembers.Add(
            membership);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}