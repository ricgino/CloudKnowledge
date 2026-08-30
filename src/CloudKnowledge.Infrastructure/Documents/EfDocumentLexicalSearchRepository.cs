using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class EfDocumentLexicalSearchRepository
    : IDocumentLexicalSearchRepository
{
    private readonly CloudKnowledgeDbContext
        _dbContext;

    private readonly DocumentRetrievalScopeQuery
        _scopeQuery;

    public EfDocumentLexicalSearchRepository(
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

    public async Task<IReadOnlyList<LexicalSearchResult>> SearchAccessibleAsync(
        Guid userId,
        string query,
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

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException(
                "Search query cannot be empty.",
                nameof(query));
        }

        if (take < 1 || take > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                "Take must be between 1 and 20.");
        }

        ArgumentNullException.ThrowIfNull(
            scope);

        var normalizedQuery =
            string.Join(
                ' ',
                query.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));

        var accessibleDocuments =
            await _scopeQuery.CreateAsync(
                userId,
                scope,
                cancellationToken);

        try
        {
            var rows =
                await (
                    from chunk
                        in _dbContext.DocumentChunks
                            .AsNoTracking()

                    join document
                        in accessibleDocuments

                        on chunk.DocumentId
                        equals document.Id

                    let searchVector =
                        EF.Property<NpgsqlTsVector>(
                            chunk,
                            "SearchVector")

                    where searchVector.Matches(
                        EF.Functions.WebSearchToTsQuery(
                            "simple",
                            normalizedQuery))

                    orderby searchVector
                        .RankCoverDensity(
                            EF.Functions.WebSearchToTsQuery(
                                "simple",
                                normalizedQuery))
                        descending

                    select new
                    {
                        chunk.DocumentId,

                        ChunkId =
                            chunk.Id,

                        chunk.Position,
                        chunk.Content,

                        Rank =
                            searchVector
                                .RankCoverDensity(
                                    EF.Functions.WebSearchToTsQuery(
                                        "simple",
                                        normalizedQuery))
                    })
                    .Take(take)
                    .ToListAsync(
                        cancellationToken);

            return rows
                .Select(
                    row =>
                        new LexicalSearchResult(
                            row.DocumentId,
                            row.ChunkId,
                            row.Position,
                            row.Content,
                            row.Rank))
                .ToArray();
        }
        catch (PostgresException exception)
            when (exception.SqlState == "42601")
        {
            throw new LexicalQuerySyntaxException(
                "PostgreSQL could not parse the lexical retrieval query.",
                exception);
        }
    }
}
