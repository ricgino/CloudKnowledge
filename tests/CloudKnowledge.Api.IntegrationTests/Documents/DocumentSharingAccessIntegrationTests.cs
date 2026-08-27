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
                dbContext,
                new EfTeamScopeResolver(
                    dbContext));

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

        Assert.False(
            await documentAccessRepository.CanAccessAsync(
                bob.Id,
                document.Id,
                CancellationToken.None));

        var shareResult =
            await shareUseCase.ExecuteAsync(
                document.Id,
                engineering.Id,
                CancellationToken.None);

        Assert.Equal(
            ShareDocumentStatus.Shared,
            shareResult);

        Assert.True(
            await dbContext.DocumentTeamAccess
                .AsNoTracking()
                .AnyAsync(
                    access =>
                        access.DocumentId == document.Id &&
                        access.TeamId == engineering.Id));

        Assert.True(
            await documentAccessRepository.CanAccessAsync(
                bob.Id,
                document.Id,
                CancellationToken.None));

        var unshareResult =
            await unshareUseCase.ExecuteAsync(
                document.Id,
                engineering.Id,
                CancellationToken.None);

        Assert.Equal(
            UnshareDocumentStatus.Unshared,
            unshareResult);

        Assert.False(
            await dbContext.DocumentTeamAccess
                .AsNoTracking()
                .AnyAsync(
                    access =>
                        access.DocumentId == document.Id &&
                        access.TeamId == engineering.Id));

        Assert.False(
            await documentAccessRepository.CanAccessAsync(
                bob.Id,
                document.Id,
                CancellationToken.None));

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
