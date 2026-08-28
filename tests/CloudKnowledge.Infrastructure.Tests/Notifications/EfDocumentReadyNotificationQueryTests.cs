using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Notifications;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Infrastructure.Tests.Notifications;

public sealed class EfDocumentReadyNotificationQueryTests
{
    [Fact]
    public async Task GetAudienceAsync_ForTeamOwnedDocument_ShouldIncludeOwningAndExplicitlySharedTeamMembers()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase("cloudknowledge_notification_audience_test")
                .WithUsername("cloudknowledge")
                .WithPassword("cloudknowledge_test")
                .Build();

        await postgres.StartAsync();

        await using var dbContext =
            await CreateDbContextAsync(
                postgres.GetConnectionString());

        var ownerMember =
            UserAccount.Create(
                "owner-member@example.com",
                "Owner Member");

        var sharedMember =
            UserAccount.Create(
                "shared-member@example.com",
                "Shared Member");

        var outsider =
            UserAccount.Create(
                "outsider@example.com",
                "Outsider");

        var owningTeam =
            Team.Create("Engineering");

        var sharedTeam =
            Team.Create("Architecture");

        dbContext.AddRange(
            ownerMember,
            sharedMember,
            outsider,
            owningTeam,
            sharedTeam);

        await dbContext.SaveChangesAsync();

        dbContext.TeamMembers.AddRange(
            TeamMember.Create(
                owningTeam.Id,
                ownerMember.Id),
            TeamMember.Create(
                sharedTeam.Id,
                sharedMember.Id));

        var document =
            Document.Create(
                "team-guide.pdf",
                "application/pdf");

        document.AssignTeamOwner(
            owningTeam.Id);

        dbContext.Documents.Add(document);

        await dbContext.SaveChangesAsync();

        dbContext.DocumentTeamAccess.Add(
            DocumentTeamAccess.Create(
                document.Id,
                sharedTeam.Id));

        await dbContext.SaveChangesAsync();

        var query =
            new EfDocumentReadyNotificationQuery(
                dbContext);

        var audience =
            await query.GetAudienceAsync(
                document.Id,
                CancellationToken.None);

        Assert.NotNull(audience);
        Assert.Equal(
            Guid.Empty,
            audience.OwnerUserId);
        Assert.Equal(
            "Engineering",
            audience.OwnerDisplayName);

        var expectedRecipients =
            new[]
            {
                ownerMember.Id,
                sharedMember.Id
            }
            .OrderBy(id => id)
            .ToArray();

        Assert.Equal(
            expectedRecipients,
            audience.RecipientUserIds
                .OrderBy(id => id)
                .ToArray());

        Assert.DoesNotContain(
            outsider.Id,
            audience.RecipientUserIds);
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
            new CloudKnowledgeDbContext(
                options);

        await dbContext.Database.MigrateAsync();

        return dbContext;
    }
}
