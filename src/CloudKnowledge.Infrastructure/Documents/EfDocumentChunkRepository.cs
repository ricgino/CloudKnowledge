using CloudKnowledge.Application.Documents;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentChunkRepository
    : IDocumentChunkRepository
{
    private readonly CloudKnowledgeDbContext _dbContext;

    public EfDocumentChunkRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ReplaceForDocumentAsync(
        Guid documentId,
        IReadOnlyCollection<DocumentChunk> chunks,
        CancellationToken cancellationToken)
    {
        var existingChunks =
            await _dbContext.DocumentChunks
                .Where(
                    chunk =>
                        chunk.DocumentId == documentId)
                .ToListAsync(
                    cancellationToken);

        _dbContext.DocumentChunks.RemoveRange(
            existingChunks);

        await _dbContext.DocumentChunks.AddRangeAsync(
            chunks,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}