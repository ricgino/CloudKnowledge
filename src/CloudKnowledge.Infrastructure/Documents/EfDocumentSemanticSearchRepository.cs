using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentSemanticSearchRepository
    : IDocumentSemanticSearchRepository
{
    private readonly CloudKnowledgeDbContext _dbContext;

    public EfDocumentSemanticSearchRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int take,
        CancellationToken cancellationToken)
    {
        return SearchInternalAsync(
            accessibleUserId: null,
            queryEmbedding,
            take,
            cancellationToken);
    }

    public Task<IReadOnlyList<SemanticSearchResult>> SearchAccessibleAsync(
        Guid userId,
        float[] queryEmbedding,
        int take,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User id cannot be empty.",
                nameof(userId));
        }

        return SearchInternalAsync(
            userId,
            queryEmbedding,
            take,
            cancellationToken);
    }

    private async Task<IReadOnlyList<SemanticSearchResult>> SearchInternalAsync(
        Guid? accessibleUserId,
        float[] queryEmbedding,
        int take,
        CancellationToken cancellationToken)
    {
        var queryVector =
            new Vector(
                queryEmbedding);

        var documents =
            _dbContext.Documents
                .AsNoTracking();

        if (accessibleUserId is not null)
        {
            documents =
                documents.WhereAccessibleTo(
                    _dbContext,
                    accessibleUserId.Value);
        }

        var rows =
            await (
                from embedding
                    in _dbContext.DocumentChunkEmbeddings
                        .AsNoTracking()

                join chunk
                    in _dbContext.DocumentChunks
                        .AsNoTracking()

                    on embedding.ChunkId
                    equals chunk.Id

                join document
                    in documents

                    on chunk.DocumentId
                    equals document.Id

                orderby embedding.Embedding
                    .CosineDistance(queryVector)

                select new
                {
                    chunk.DocumentId,

                    ChunkId =
                        chunk.Id,

                    chunk.Position,
                    chunk.Content,

                    Distance =
                        embedding.Embedding
                            .CosineDistance(
                                queryVector)
                })
                .Take(take)
                .ToListAsync(
                    cancellationToken);

        return rows
            .Select(
                row =>
                    new SemanticSearchResult(
                        row.DocumentId,
                        row.ChunkId,
                        row.Position,
                        row.Content,
                        row.Distance))
            .ToArray();
    }
}