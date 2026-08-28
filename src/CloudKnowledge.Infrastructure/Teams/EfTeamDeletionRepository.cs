using CloudKnowledge.Application.Teams.DeleteTeam;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Teams;

public sealed class EfTeamDeletionRepository
    : ITeamDeletionRepository
{
    private readonly CloudKnowledgeDbContext _dbContext;

    public EfTeamDeletionRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> HasChildrenAsync(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Teams
            .AsNoTracking()
            .AnyAsync(
                team => team.ParentTeamId == teamId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetOwnedDocumentIdsAsync(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .Where(
                document => document.OwnerTeamId == teamId)
            .OrderBy(document => document.Id)
            .Select(document => document.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task DeleteLeafAsync(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        await _dbContext.Teams
            .Where(team => team.Id == teamId)
            .ExecuteDeleteAsync(
                cancellationToken);
    }
}
