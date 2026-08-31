using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class LexicalSearchQuerySemanticsTests
{
    [Fact]
    public async Task SearchAccessibleAsync_ShouldFindTechnicalEvidence_WhenQueryContainsProductIdentifierAndExtraIntentTerms()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase(
                    "cloudknowledge_lexical_query_semantics_test")
                .WithUsername(
                    "cloudknowledge")
                .WithPassword(
                    "cloudknowledge_test")
                .Build();

        await postgres.StartAsync();

        var options =
            new DbContextOptionsBuilder<CloudKnowledgeDbContext>()
                .UseNpgsql(
                    postgres.GetConnectionString(),
                    npgsqlOptions =>
                        npgsqlOptions.UseVector())
                .Options;

        await using var dbContext =
            new CloudKnowledgeDbContext(
                options);

        await dbContext.Database
            .MigrateAsync();

        var user =
            UserAccount.Create(
                "lexical-query@example.com",
                "Lexical Query User");

        var document =
            Document.Create(
                "acs880-hardware-manual.pdf",
                "application/pdf");

        document.AssignOwner(
            user.Id);

        var evidenceChunk =
            DocumentChunk.Create(
                document.Id,
                0,
                "Installation site altitude: 0 to 4000 m. At altitudes from 1000 to 4000 m above sea level, the rated output current is decreased by 1% for every 100 m.");

        var navigationDocument =
            Document.Create(
                "acs880-related-manuals.pdf",
                "application/pdf");

        navigationDocument.AssignOwner(
            user.Id);

        var navigationChunks =
            Enumerable.Range(0, 8)
                .Select(
                    index =>
                        DocumentChunk.Create(
                            navigationDocument.Id,
                            index,
                            string.Join(
                                ' ',
                                Enumerable.Repeat(
                                    "ACS880-01 related manuals hardware manual product documentation drives",
                                    80))))
                .ToArray();

        dbContext.AddRange(
            user,
            document,
            navigationDocument,
            evidenceChunk);

        dbContext.AddRange(
            navigationChunks);

        await dbContext.SaveChangesAsync();

        var repository =
            new EfDocumentLexicalSearchRepository(
                dbContext,
                new EfTeamScopeResolver(
                    dbContext));

        var results =
            await repository.SearchAccessibleAsync(
                user.Id,
                "ACS880-01 rated current at high altitude",
                5,
                DocumentRetrievalScope.All,
                CancellationToken.None);

        Assert.NotEmpty(
            results);

        Assert.Equal(
            evidenceChunk.Id,
            results[0].ChunkId);
    }
}
