using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentSemanticSearchRepository
    : IDocumentSemanticSearchRepository
{
    private readonly CloudKnowledgeDbContext
        _dbContext;

    private readonly ITeamScopeResolver
        _teamScopeResolver;

    public EfDocumentSemanticSearchRepository(
        CloudKnowledgeDbContext dbContext,
        ITeamScopeResolver teamScopeResolver)
    {
        _dbContext =
            dbContext;

        _teamScopeResolver =
            teamScopeResolver;
    }

    public Task<IReadOnlyList<SemanticSearchResult>>
        SearchAccessibleAsync(
            Guid userId,
            float[] queryEmbedding,
            int take,
            CancellationToken cancellationToken)
    {
        return SearchAccessibleAsync(
            userId,
            queryEmbedding,
            take,
            DocumentRetrievalScope.All,
            cancellationToken);
    }

    public async Task<IReadOnlyList<SemanticSearchResult>>
        SearchAccessibleAsync(
            Guid userId,
            float[] queryEmbedding,
            int take,
            DocumentRetrievalScope scope,
            CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User id cannot be empty.",
                nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(
            scope);

        var queryVector =
            new Vector(
                queryEmbedding);

        IQueryable<Document> accessibleDocuments =
            _dbContext.Documents
                .AsNoTracking()
                .WhereAccessibleTo(
                    _dbContext,
                    userId);

        switch (scope.Kind)
        {
            case DocumentRetrievalScopeKind.All:
                break;

            case DocumentRetrievalScopeKind.Team:
                var allowedTeamIds =
                    await _teamScopeResolver.ResolveAllowedTeamIdsAsync(
                        userId,
                        scope.TeamId!.Value,
                        scope.IncludeDescendants,
                        cancellationToken);

                if (allowedTeamIds.Length == 0)
                {
                    accessibleDocuments =
                        accessibleDocuments.Where(
                            _ => false);
                    break;
                }

                accessibleDocuments =
                    accessibleDocuments.Where(
                        document =>
                            (document.OwnerTeamId.HasValue
                             && allowedTeamIds.Contains(
                                 document.OwnerTeamId.Value))

                            ||

                            _dbContext.DocumentTeamAccess.Any(
                                access =>
                                    access.DocumentId == document.Id
                                    && allowedTeamIds.Contains(
                                        access.TeamId)));
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scope),
                    scope.Kind,
                    "Unknown document retrieval scope.");
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
                    in accessibleDocuments

                    on chunk.DocumentId
                    equals document.Id

                orderby embedding.Embedding
                    .CosineDistance(
                        queryVector)

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
