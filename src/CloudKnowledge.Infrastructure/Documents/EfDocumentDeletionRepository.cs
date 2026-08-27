using CloudKnowledge.Application.Documents.DeleteDocument;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentDeletionRepository
    : IDocumentDeletionRepository
{
    private readonly CloudKnowledgeDbContext _dbContext;

    public EfDocumentDeletionRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> DeleteOwnedAsync(
        Guid ownerUserId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document =
            await _dbContext.Documents
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == documentId &&
                        item.OwnerUserId == ownerUserId,
                    cancellationToken);

        if (document is null)
        {
            return false;
        }

        _dbContext.Documents.Remove(document);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return true;
    }
}
