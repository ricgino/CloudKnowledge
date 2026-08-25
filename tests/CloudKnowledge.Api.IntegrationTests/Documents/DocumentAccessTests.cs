using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class DocumentAccessTests
{
    [Fact]
    public async Task CanAccessAsync_ShouldRespectOwnershipAndTeamSharing()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase(
                    "cloudknowledge_access_test")
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

        var charlie =
            UserAccount.Create(
                "charlie@example.com",
                "Charlie");

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

        var sharedAccess =
            DocumentTeamAccess.Create(
                aliceSharedDocument.Id,
                team.Id);

        dbContext.AddRange(
            alice,
            bob,
            charlie,
            team,
            aliceMembership,
            bobMembership,
            alicePrivateDocument,
            aliceSharedDocument,
            bobPrivateDocument,
            sharedAccess);

        await dbContext.SaveChangesAsync();

        var repository =
            new EfDocumentAccessRepository(
                dbContext);

        // Alice owns both of her documents.
        Assert.True(
            await repository.CanAccessAsync(
                alice.Id,
                alicePrivateDocument.Id,
                CancellationToken.None));

        Assert.True(
            await repository.CanAccessAsync(
                alice.Id,
                aliceSharedDocument.Id,
                CancellationToken.None));

        // Bob cannot access Alice's private document.
        Assert.False(
            await repository.CanAccessAsync(
                bob.Id,
                alicePrivateDocument.Id,
                CancellationToken.None));

        // Bob is in Engineering,
        // therefore he can access the shared document.
        Assert.True(
            await repository.CanAccessAsync(
                bob.Id,
                aliceSharedDocument.Id,
                CancellationToken.None));

        // Bob can access his own private document.
        Assert.True(
            await repository.CanAccessAsync(
                bob.Id,
                bobPrivateDocument.Id,
                CancellationToken.None));

        // Charlie belongs to no team.
        Assert.False(
            await repository.CanAccessAsync(
                charlie.Id,
                alicePrivateDocument.Id,
                CancellationToken.None));

        Assert.False(
            await repository.CanAccessAsync(
                charlie.Id,
                aliceSharedDocument.Id,
                CancellationToken.None));
    }
}