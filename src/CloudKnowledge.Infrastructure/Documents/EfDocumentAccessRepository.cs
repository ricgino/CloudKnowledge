using CloudKnowledge.Application.Documents.Access;
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

    public async Task<bool> CanAccessAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document id cannot be empty.",
                nameof(documentId));
        }

        return await _dbContext.Documents
            .AsNoTracking()
            .Where(document =>
                document.Id == documentId)
            .WhereAccessibleTo(
                _dbContext,
                userId)
            .AnyAsync(
                cancellationToken);
    }
}