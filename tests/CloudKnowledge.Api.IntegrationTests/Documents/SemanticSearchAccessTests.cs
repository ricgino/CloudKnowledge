using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class SemanticSearchAccessTests
{
    [Fact]
    public async Task SearchAccessibleAsync_ShouldNeverReturnUnauthorizedDocuments()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase(
                    "cloudknowledge_search_access_test")
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

        var alice =
            UserAccount.Create(
                "alice@example.com",
                "Alice");

        var bob =
            UserAccount.Create(
                "bob@example.com",
                "Bob");

        var team =
            Team.Create(
                "Engineering");

        var aliceMembership =
            TeamMember.Create(
                team.Id,
                alice.Id,
                TeamRole.Owner);

        var bobMembership =
            TeamMember.Create(
                team.Id,
                bob.Id,
                TeamRole.Member);

        var alicePrivateDocument =
            Document.Create(
                "alice-private.pdf",
                "application/pdf");

        alicePrivateDocument.AssignOwner(
            alice.Id);

        var aliceSharedDocument =
            Document.Create(
                "alice-shared.pdf",
                "application/pdf");

        aliceSharedDocument.AssignOwner(
            alice.Id);

        var bobPrivateDocument =
            Document.Create(
                "bob-private.pdf",
                "application/pdf");

        bobPrivateDocument.AssignOwner(
            bob.Id);

        var alicePrivateChunk =
            DocumentChunk.Create(
                alicePrivateDocument.Id,
                0,
                "TOP SECRET ALICE");

        var aliceSharedChunk =
            DocumentChunk.Create(
                aliceSharedDocument.Id,
                0,
                "SHARED TEAM INFORMATION");

        var bobPrivateChunk =
            DocumentChunk.Create(
                bobPrivateDocument.Id,
                0,
                "BOB PRIVATE INFORMATION");

        var sharedAccess =
            DocumentTeamAccess.Create(
                aliceSharedDocument.Id,
                team.Id);

        dbContext.AddRange(
            alice,
            bob,
            team,
            aliceMembership,
            bobMembership,
            alicePrivateDocument,
            aliceSharedDocument,
            bobPrivateDocument,
            alicePrivateChunk,
            aliceSharedChunk,
            bobPrivateChunk,
            sharedAccess);

        await dbContext.SaveChangesAsync();

        // Query vector is deliberately almost identical
        // to Alice's PRIVATE document.
        var queryEmbedding =
            CreateVector(
                firstComponent: 1.0f);

        dbContext.DocumentChunkEmbeddings.AddRange(
            CreateEmbedding(
                alicePrivateChunk,
                CreateVector(
                    firstComponent: 1.0f)),

            CreateEmbedding(
                aliceSharedChunk,
                CreateVector(
                    firstComponent: 0.8f,
                    secondComponent: 0.2f)),

            CreateEmbedding(
                bobPrivateChunk,
                CreateVector(
                    firstComponent: 0.7f,
                    secondComponent: 0.3f)));

        await dbContext.SaveChangesAsync();

        var repository =
            new EfDocumentSemanticSearchRepository(
                dbContext);

        var results =
            await repository.SearchAccessibleAsync(
                bob.Id,
                queryEmbedding,
                10,
                CancellationToken.None);

        Assert.DoesNotContain(
            results,
            result =>
                result.DocumentId ==
                alicePrivateDocument.Id);

        Assert.Contains(
            results,
            result =>
                result.DocumentId ==
                aliceSharedDocument.Id);

        Assert.Contains(
            results,
            result =>
                result.DocumentId ==
                bobPrivateDocument.Id);

        Assert.All(
            results,
            result =>
                Assert.NotEqual(
                    "TOP SECRET ALICE",
                    result.Content));
    }

    private static float[] CreateVector(
        float firstComponent,
        float secondComponent = 0.0f)
    {
        var vector =
            new float[768];

        vector[0] =
            firstComponent;

        vector[1] =
            secondComponent;

        return vector;
    }

    private static DocumentChunkEmbeddingRow CreateEmbedding(
        DocumentChunk chunk,
        float[] values)
    {
        return new DocumentChunkEmbeddingRow
        {
            ChunkId =
                chunk.Id,

            DocumentId =
                chunk.DocumentId,

            Embedding =
                new Vector(
                    values)
        };
    }
}