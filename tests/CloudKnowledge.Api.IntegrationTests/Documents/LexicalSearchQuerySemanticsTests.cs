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
                "Altitude derating: Above 1000 m, output current requires derating for increasing installation altitude.");

        dbContext.AddRange(
            user,
            document,
            evidenceChunk);

        await dbContext.SaveChangesAsync();

        var repository =
            new EfDocumentLexicalSearchRepository(
                dbContext,
                new EfTeamScopeResolver(
                    dbContext));

        var results =
            await repository.SearchAccessibleAsync(
                user.Id,
                "ACS880-01 altitude derating limitations",
                10,
                DocumentRetrievalScope.All,
                CancellationToken.None);

        Assert.Contains(
            results,
            result =>
                result.ChunkId ==
                evidenceChunk.Id);
    }
}
