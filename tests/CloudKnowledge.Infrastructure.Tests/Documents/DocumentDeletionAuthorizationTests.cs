using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Infrastructure.Tests.Documents;

public sealed class DocumentDeletionAuthorizationTests
{
    [Fact]
    public async Task DeleteAuthorizedAsync_ShouldRequirePersonalOwnershipOrDirectTeamOwnerRole()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase("cloudknowledge_document_delete_test")
                .WithUsername("cloudknowledge")
                .WithPassword("cloudknowledge_test")
                .Build();

        await postgres.StartAsync();

        await using var dbContext =
            await CreateDbContextAsync(
                postgres.GetConnectionString());

        var teamOwner =
            UserAccount.Create(
                "team.owner@example.com",
                "Team Owner");

        var teamAdmin =
            UserAccount.Create(
                "team.admin@example.com",
                "Team Admin");

        var teamMember =
            UserAccount.Create(
                "team.member@example.com",
                "Team Member");

        var unrelated =
            UserAccount.Create(
                "unrelated@example.com",
                "Unrelated User");

        var otherOwner =
            UserAccount.Create(
                "other.owner@example.com",
                "Other Owner");

        var dota =
            Team.Create("Dota");

        dbContext.UserAccounts.AddRange(
            teamOwner,
            teamAdmin,
            teamMember,
            unrelated,
            otherOwner);

        dbContext.Teams.Add(dota);
        await dbContext.SaveChangesAsync();

        dbContext.TeamMembers.AddRange(
            TeamMember.Create(
                dota.Id,
                teamOwner.Id,
                TeamRole.Owner),
            TeamMember.Create(
                dota.Id,
                teamAdmin.Id,
                TeamRole.Admin),
            TeamMember.Create(
                dota.Id,
                teamMember.Id,
                TeamRole.Member));

        var personal =
            CreateUserOwnedDocument(
                "personal.pdf",
                teamOwner.Id);

        var teamOwnedForOwner =
            CreateTeamOwnedDocument(
                "team-owner.pdf",
                dota.Id);

        var teamOwnedForAdmin =
            CreateTeamOwnedDocument(
                "team-admin.pdf",
                dota.Id);

        var teamOwnedForMember =
            CreateTeamOwnedDocument(
                "team-member.pdf",
                dota.Id);

        var teamOwnedForUnrelated =
            CreateTeamOwnedDocument(
                "team-unrelated.pdf",
                dota.Id);

        var sharedOnly =
            CreateUserOwnedDocument(
                "shared-only.pdf",
                otherOwner.Id);

        dbContext.Documents.AddRange(
            personal,
            teamOwnedForOwner,
            teamOwnedForAdmin,
            teamOwnedForMember,
            teamOwnedForUnrelated,
            sharedOnly);

        await dbContext.SaveChangesAsync();

        dbContext.DocumentTeamAccess.Add(
            DocumentTeamAccess.Create(
                sharedOnly.Id,
                dota.Id));

        await dbContext.SaveChangesAsync();

        var repository =
            new EfDocumentDeletionRepository(
                dbContext);

        Assert.True(
            await repository.DeleteAuthorizedAsync(
                teamOwner.Id,
                personal.Id,
                CancellationToken.None));

        Assert.True(
            await repository.DeleteAuthorizedAsync(
                teamOwner.Id,
                teamOwnedForOwner.Id,
                CancellationToken.None));

        Assert.False(
            await repository.DeleteAuthorizedAsync(
                teamAdmin.Id,
                teamOwnedForAdmin.Id,
                CancellationToken.None));

        Assert.False(
            await repository.DeleteAuthorizedAsync(
                teamMember.Id,
                teamOwnedForMember.Id,
                CancellationToken.None));

        Assert.False(
            await repository.DeleteAuthorizedAsync(
                unrelated.Id,
                teamOwnedForUnrelated.Id,
                CancellationToken.None));

        Assert.False(
            await repository.DeleteAuthorizedAsync(
                teamOwner.Id,
                sharedOnly.Id,
                CancellationToken.None));
    }

    private static Document CreateUserOwnedDocument(
        string fileName,
        Guid ownerUserId)
    {
        var document =
            Document.Create(
                fileName,
                "application/pdf");

        document.AssignUserOwner(ownerUserId);
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

        document.AssignTeamOwner(ownerTeamId);
        return document;
    }

    private static async Task<CloudKnowledgeDbContext> CreateDbContextAsync(
        string connectionString)
    {
        var options =
            new DbContextOptionsBuilder<CloudKnowledgeDbContext>()
                .UseNpgsql(
                    connectionString,
                    npgsqlOptions =>
                        npgsqlOptions.UseVector())
                .Options;

        var dbContext =
            new CloudKnowledgeDbContext(options);

        await dbContext.Database.MigrateAsync();
        return dbContext;
    }
}
