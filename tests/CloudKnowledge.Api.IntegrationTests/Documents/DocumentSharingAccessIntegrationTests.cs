using CloudKnowledge.Application.Documents.Sharing;
using CloudKnowledge.Application.Notifications.DocumentReady;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class DocumentSharingAccessIntegrationTests
{
    [Fact]
    public async Task ShareAndUnshare_ShouldGrantAndRevokeTeamAccess()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase(
                    "cloudknowledge_sharing_test")
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

        var engineering =
            Team.Create(
                "Engineering");

        var aliceMembership =
            TeamMember.Create(
                engineering.Id,
                alice.Id,
                TeamRole.Owner);

        var bobMembership =
            TeamMember.Create(
                engineering.Id,
                bob.Id,
                TeamRole.Member);

        var document =
            Document.Create(
                "architecture.pdf",
                "application/pdf");

        document.AssignOwner(
            alice.Id);

        dbContext.AddRange(
            alice,
            bob,
            engineering,
            aliceMembership,
            bobMembership,
            document);

        await dbContext.SaveChangesAsync();

        var documentSharingRepository =
            new EfDocumentSharingRepository(
                dbContext);

        var teamMembershipRepository =
            new EfTeamMembershipRepository(
                dbContext);

        var documentAccessRepository =
            new EfDocumentAccessRepository(
                dbContext);

        var documentRepository =
            new EfDocumentRepository(
                dbContext);

        var documentReadyPublisher =
            new FakeDocumentReadyPublisher();

        var currentUser =
            new FakeCurrentUser(
                alice.Id);

        var shareUseCase =
            new ShareDocumentWithTeamUseCase(
                documentSharingRepository,
                teamMembershipRepository,
                documentRepository,
                documentReadyPublisher,
                currentUser);

        var unshareUseCase =
            new UnshareDocumentFromTeamUseCase(
                documentSharingRepository,
                teamMembershipRepository,
                currentUser);

        // Bob is in the team,
        // but the document is still private.
        Assert.False(
            await documentAccessRepository.CanAccessAsync(
                bob.Id,
                document.Id,
                CancellationToken.None));

        // Alice owns the document and belongs to the team,
        // so she can share it.
        var shareResult =
            await shareUseCase.ExecuteAsync(
                document.Id,
                engineering.Id,
                CancellationToken.None);

        Assert.Equal(
            ShareDocumentStatus.Shared,
            shareResult);

        // Sharing must now exist in the real database.
        Assert.True(
            await dbContext.DocumentTeamAccess
                .AsNoTracking()
                .AnyAsync(
                    access =>
                        access.DocumentId == document.Id &&
                        access.TeamId == engineering.Id));

        // Bob immediately gains access through team membership.
        Assert.True(
            await documentAccessRepository.CanAccessAsync(
                bob.Id,
                document.Id,
                CancellationToken.None));

        // Alice revokes the share.
        var unshareResult =
            await unshareUseCase.ExecuteAsync(
                document.Id,
                engineering.Id,
                CancellationToken.None);

        Assert.Equal(
            UnshareDocumentStatus.Unshared,
            unshareResult);

        // The sharing row must really be gone.
        Assert.False(
            await dbContext.DocumentTeamAccess
                .AsNoTracking()
                .AnyAsync(
                    access =>
                        access.DocumentId == document.Id &&
                        access.TeamId == engineering.Id));

        // Bob loses access again.
        Assert.False(
            await documentAccessRepository.CanAccessAsync(
                bob.Id,
                document.Id,
                CancellationToken.None));

        // Alice never loses access because she is the owner.
        Assert.True(
            await documentAccessRepository.CanAccessAsync(
                alice.Id,
                document.Id,
                CancellationToken.None));
    }

    private sealed class FakeCurrentUser
        : ICurrentUser
    {
        private readonly Guid
            _userId;

        public FakeCurrentUser(
            Guid userId)
        {
            _userId =
                userId;
        }

        public Task<Guid> GetUserIdAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _userId);
        }
    }

    private sealed class FakeDocumentReadyPublisher
        : IDocumentReadyPublisher
    {
        public Task PublishAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
