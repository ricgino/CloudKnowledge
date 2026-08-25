using CloudKnowledge.Application.Documents.Sharing;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentSharingRepository
    : IDocumentSharingRepository
{
    private readonly CloudKnowledgeDbContext
        _dbContext;

    public EfDocumentSharingRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task<bool> IsOwnedByAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .AnyAsync(
                document =>
                    document.Id == documentId &&
                    document.OwnerUserId == userId,
                cancellationToken);
    }

    public async Task<bool> IsSharedWithTeamAsync(
        Guid documentId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.DocumentTeamAccess
            .AsNoTracking()
            .AnyAsync(
                access =>
                    access.DocumentId == documentId &&
                    access.TeamId == teamId,
                cancellationToken);
    }

    public async Task AddAsync(
        DocumentTeamAccess access,
        CancellationToken cancellationToken)
    {
        _dbContext.DocumentTeamAccess.Add(
            access);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task RemoveAsync(
        Guid documentId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var access =
            await _dbContext.DocumentTeamAccess
                .SingleOrDefaultAsync(
                    item =>
                        item.DocumentId == documentId &&
                        item.TeamId == teamId,
                    cancellationToken);

        if (access is null)
        {
            return;
        }

        _dbContext.DocumentTeamAccess.Remove(
            access);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}