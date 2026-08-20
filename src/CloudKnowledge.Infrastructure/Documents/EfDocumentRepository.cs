using CloudKnowledge.Application.Documents;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentRepository : IDocumentRepository
{
    private readonly CloudKnowledgeDbContext _dbContext;

    public EfDocumentRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        _dbContext.Documents.Add(document);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<Document?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                document => document.Id == id,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetPageAsync(
    int skip,
    int take,
    CancellationToken cancellationToken)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .OrderByDescending(document => document.CreatedAtUtc)
            .ThenBy(document => document.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.Documents
            .CountAsync(cancellationToken);
    }
}