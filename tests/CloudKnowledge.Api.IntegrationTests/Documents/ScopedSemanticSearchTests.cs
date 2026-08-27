using CloudKnowledge.Application.Documents.SearchDocuments;
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

public sealed class ScopedSemanticSearchTests
{
    [Fact]
    public async Task TeamScope_ShouldFilterBeforeSimilarityTopN()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase(
                    "cloudknowledge_scoped_search_test")
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

        await dbContext.Database.MigrateAsync();

        var currentUser =
            UserAccount.Create(
                "scoped.user@example.com",
                "Scoped User");

        var otherUser =
            UserAccount.Create(
                "owner@example.com",
                "Document Owner");

        var rai =
            Team.Create("Rai");

        var deskSharing =
            Team.Create(
                "DeskSharing",
                rai.Id);

        var booking =
            Team.Create(
                "Booking",
                rai.Id);

        var hrPortal =
            Team.Create(
                "HR Portal",
                rai.Id);

        dbContext.AddRange(
            currentUser,
            otherUser,
            rai,
            deskSharing,
            booking,
            hrPortal,
            TeamMember.Create(
                deskSharing.Id,
                currentUser.Id),
            TeamMember.Create(
                hrPortal.Id,
                currentUser.Id));

        var personal =
            CreateUserOwnedDocument(
                "personal.pdf",
                currentUser.Id);

        var deskShared =
            CreateUserOwnedDocument(
                "desk-shared.pdf",
                otherUser.Id);

        var deskOwned =
            CreateTeamOwnedDocument(
                "desk-owned.pdf",
                deskSharing.Id);

        var hrShared =
            CreateUserOwnedDocument(
                "hr-shared.pdf",
                otherUser.Id);

        var bookingSecret =
            CreateUserOwnedDocument(
                "booking-secret.pdf",
                otherUser.Id);

        dbContext.Documents.AddRange(
            personal,
            deskShared,
            deskOwned,
            hrShared,
            bookingSecret);

        await dbContext.SaveChangesAsync();

        dbContext.DocumentTeamAccess.AddRange(
            DocumentTeamAccess.Create(
                deskShared.Id,
                deskSharing.Id),
            DocumentTeamAccess.Create(
                hrShared.Id,
                hrPortal.Id),
            DocumentTeamAccess.Create(
                bookingSecret.Id,
                booking.Id));

        var personalChunk =
            DocumentChunk.Create(
                personal.Id,
                0,
                "PERSONAL STRONGEST MATCH");

        var deskSharedChunk =
            DocumentChunk.Create(
                deskShared.Id,
                0,
                "DESK SHARED");

        var deskOwnedChunk =
            DocumentChunk.Create(
                deskOwned.Id,
                0,
                "DESK OWNED");

        var hrChunk =
            DocumentChunk.Create(
                hrShared.Id,
                0,
                "HR PORTAL");

        var bookingChunk =
            DocumentChunk.Create(
                bookingSecret.Id,
                0,
                "BOOKING SECRET");

        dbContext.DocumentChunks.AddRange(
            personalChunk,
            deskSharedChunk,
            deskOwnedChunk,
            hrChunk,
            bookingChunk);

        await dbContext.SaveChangesAsync();

        dbContext.DocumentChunkEmbeddings.AddRange(
            CreateEmbedding(
                personalChunk,
                CreateVector(1.0f, 0.0f)),
            CreateEmbedding(
                bookingChunk,
                CreateVector(0.99f, 0.01f)),
            CreateEmbedding(
                hrChunk,
                CreateVector(0.90f, 0.10f)),
            CreateEmbedding(
                deskSharedChunk,
                CreateVector(0.85f, 0.15f)),
            CreateEmbedding(
                deskOwnedChunk,
                CreateVector(0.80f, 0.20f)));

        await dbContext.SaveChangesAsync();

        var repository =
            new EfDocumentSemanticSearchRepository(
                dbContext);

        var queryEmbedding =
            CreateVector(1.0f, 0.0f);

        var global =
            await repository.SearchAccessibleAsync(
                currentUser.Id,
                queryEmbedding,
                20,
                DocumentRetrievalScope.All,
                CancellationToken.None);

        Assert.Contains(
            global,
            result => result.DocumentId == personal.Id);
        Assert.Contains(
            global,
            result => result.DocumentId == deskShared.Id);
        Assert.Contains(
            global,
            result => result.DocumentId == deskOwned.Id);
        Assert.Contains(
            global,
            result => result.DocumentId == hrShared.Id);
        Assert.DoesNotContain(
            global,
            result => result.DocumentId == bookingSecret.Id);

        var deskOnly =
            await repository.SearchAccessibleAsync(
                currentUser.Id,
                queryEmbedding,
                20,
                DocumentRetrievalScope.ForTeam(
                    deskSharing.Id,
                    includeDescendants: false),
                CancellationToken.None);

        Assert.Equal(
            new[]
            {
                deskShared.Id,
                deskOwned.Id
            }.OrderBy(id => id),
            deskOnly
                .Select(result => result.DocumentId)
                .OrderBy(id => id));

        var raiBranch =
            await repository.SearchAccessibleAsync(
                currentUser.Id,
                queryEmbedding,
                20,
                DocumentRetrievalScope.ForTeam(
                    rai.Id,
                    includeDescendants: true),
                CancellationToken.None);

        Assert.Contains(
            raiBranch,
            result => result.DocumentId == deskShared.Id);
        Assert.Contains(
            raiBranch,
            result => result.DocumentId == deskOwned.Id);
        Assert.Contains(
            raiBranch,
            result => result.DocumentId == hrShared.Id);
        Assert.DoesNotContain(
            raiBranch,
            result => result.DocumentId == personal.Id);
        Assert.DoesNotContain(
            raiBranch,
            result => result.DocumentId == bookingSecret.Id);

        var deskTopOne =
            await repository.SearchAccessibleAsync(
                currentUser.Id,
                queryEmbedding,
                1,
                DocumentRetrievalScope.ForTeam(
                    deskSharing.Id,
                    includeDescendants: false),
                CancellationToken.None);

        Assert.Single(deskTopOne);
        Assert.Equal(
            deskShared.Id,
            deskTopOne[0].DocumentId);
    }

    private static Document CreateUserOwnedDocument(
        string fileName,
        Guid ownerUserId)
    {
        var document =
            Document.Create(
                fileName,
                "application/pdf");

        document.AssignOwner(
            ownerUserId);

        return document;
    }

    private static Document CreateTeamOwnedDocument(
        string fileName,
        Guid ownerTeamId)
    {
        var document =
            Document.Create(
                fileName,
                "application/pdf");

        document.AssignTeamOwner(
            ownerTeamId);

        return document;
    }

    private static float[] CreateVector(
        float firstComponent,
        float secondComponent)
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
            ChunkId = chunk.Id,
            DocumentId = chunk.DocumentId,
            Embedding = new Vector(values)
        };
    }
}
