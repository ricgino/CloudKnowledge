using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentAccessRepository
    : IDocumentAccessRepository
{
    private readonly CloudKnowledgeDbContext
        _dbContext;

    public EfDocumentAccessRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext =
            dbContext;
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
            .WhereAccessibleTo(
                _dbContext,
                userId)
            .AnyAsync(
                document =>
                    document.Id == documentId,
                cancellationToken);
    }

    public async Task<Document?> GetByIdAsync(
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
            .WhereAccessibleTo(
                _dbContext,
                userId)
            .SingleOrDefaultAsync(
                document =>
                    document.Id == documentId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetPageAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .WhereAccessibleTo(
                _dbContext,
                userId)
            .OrderByDescending(
                document =>
                    document.CreatedAtUtc)
            .ThenBy(
                document =>
                    document.Id)
            .Skip(
                skip)
            .Take(
                take)
            .ToListAsync(
                cancellationToken);
    }

    public async Task<int> CountAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .WhereAccessibleTo(
                _dbContext,
                userId)
            .CountAsync(
                cancellationToken);
    }
}