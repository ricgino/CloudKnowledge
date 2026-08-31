using System.Text.RegularExpressions;
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
    private const int MinimumCandidatePool =
        40;

    private const int MaximumCandidatePool =
        100;

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

        var queryTerms =
            ExtractTerms(
                normalizedQuery);

        var lexicalQuery =
            BuildRecallOrientedQuery(
                normalizedQuery,
                queryTerms);

        var candidateTake =
            Math.Min(
                MaximumCandidatePool,
                Math.Max(
                    MinimumCandidatePool,
                    take * 5));

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
                            lexicalQuery))

                    orderby searchVector
                        .RankCoverDensity(
                            EF.Functions.WebSearchToTsQuery(
                                "simple",
                                lexicalQuery))
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
                                        lexicalQuery))
                    })
                    .Take(candidateTake)
                    .ToListAsync(
                        cancellationToken);

            return rows
                .Select(
                    row =>
                        new
                        {
                            Row = row,
                            Coverage = CountMatchedTerms(
                                row.Content,
                                queryTerms)
                        })
                .OrderByDescending(
                    item =>
                        item.Coverage)
                .ThenByDescending(
                    item =>
                        item.Row.Rank)
                .ThenBy(
                    item =>
                        item.Row.ChunkId)
                .Take(take)
                .Select(
                    item =>
                        new LexicalSearchResult(
                            item.Row.DocumentId,
                            item.Row.ChunkId,
                            item.Row.Position,
                            item.Row.Content,
                            item.Row.Rank))
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

    private static IReadOnlyList<string> ExtractTerms(
        string query)
    {
        return Regex.Matches(
                query,
                @"[\p{L}\p{N}]+")
            .Select(
                match =>
                    match.Value)
            .Where(
                term =>
                    term.Length >= 3 ||
                    (term.Any(char.IsLetter) &&
                     term.Any(char.IsDigit)))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildRecallOrientedQuery(
        string query,
        IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return query;
        }

        return string.Join(
            " OR ",
            terms);
    }

    private static int CountMatchedTerms(
        string content,
        IReadOnlyList<string> queryTerms)
    {
        if (queryTerms.Count == 0)
        {
            return 0;
        }

        var contentTerms =
            Regex.Matches(
                    content,
                    @"[\p{L}\p{N}]+")
                .Select(
                    match =>
                        match.Value)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        return queryTerms.Count(
            contentTerms.Contains);
    }
}
