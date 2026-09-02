using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentChunkContextRepository
    : IDocumentChunkContextRepository
{
    private readonly CloudKnowledgeDbContext
        _dbContext;

    private readonly DocumentRetrievalScopeQuery
        _scopeQuery;

    public EfDocumentChunkContextRepository(
        CloudKnowledgeDbContext dbContext,
        ITeamScopeResolver teamScopeResolver)
    {
        _dbContext =
            dbContext;

        _scopeQuery =
            new DocumentRetrievalScopeQuery(
                dbContext,
                teamScopeResolver);
    }

    public async Task<DocumentChunkContextResult?> GetAccessibleNextAsync(
        Guid userId,
        Guid documentId,
        int position,
        DocumentRetrievalScope scope,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User id cannot be empty.",
                nameof(userId));
        }

        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document id cannot be empty.",
                nameof(documentId));
        }

        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position));
        }

        ArgumentNullException.ThrowIfNull(
            scope);

        var accessibleDocuments =
            await _scopeQuery.CreateAsync(
                userId,
                scope,
                cancellationToken);

        var targetPosition =
            position + 1;

        var row =
            await (
                from chunk
                    in _dbContext.DocumentChunks
                        .AsNoTracking()

                join document
                    in accessibleDocuments

                    on chunk.DocumentId
                    equals document.Id

                where chunk.DocumentId == documentId
                      && chunk.Position == targetPosition

                select new
                {
                    chunk.DocumentId,
                    ChunkId = chunk.Id,
                    chunk.Position,
                    chunk.Content
                })
                .SingleOrDefaultAsync(
                    cancellationToken);

        return row is null
            ? null
            : new DocumentChunkContextResult(
                row.DocumentId,
                row.ChunkId,
                row.Position,
                row.Content);
    }
}
