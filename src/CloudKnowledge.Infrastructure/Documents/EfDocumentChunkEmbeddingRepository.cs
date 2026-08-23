using CloudKnowledge.Application.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentChunkEmbeddingRepository
    : IDocumentChunkEmbeddingRepository
{
    private readonly CloudKnowledgeDbContext _dbContext;

    public EfDocumentChunkEmbeddingRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ReplaceForDocumentAsync(
        Guid documentId,
        IReadOnlyCollection<DocumentChunkEmbedding> embeddings,
        CancellationToken cancellationToken)
    {
        var existing =
            await _dbContext.DocumentChunkEmbeddings
                .Where(
                    embedding =>
                        embedding.DocumentId ==
                        documentId)
                .ToListAsync(
                    cancellationToken);

        _dbContext.DocumentChunkEmbeddings
            .RemoveRange(
                existing);

        var rows =
            embeddings
                .Select(
                    embedding =>
                        new DocumentChunkEmbeddingRow
                        {
                            ChunkId =
                                embedding.ChunkId,

                            DocumentId =
                                embedding.DocumentId,

                            Embedding =
                                new Vector(
                                    embedding.Values)
                        })
                .ToArray();

        await _dbContext.DocumentChunkEmbeddings
            .AddRangeAsync(
                rows,
                cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}