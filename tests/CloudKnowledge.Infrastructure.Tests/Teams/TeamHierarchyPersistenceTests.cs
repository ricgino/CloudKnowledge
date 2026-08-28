using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Infrastructure.Tests.Teams;

public sealed class TeamHierarchyPersistenceTests
{
    [Fact]
    public async Task ParentTeamId_ShouldPersistAcrossDatabaseReload()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase(
                    "cloudknowledge_team_hierarchy_test")
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

        var root =
            Team.Create(
                "Rai");

        var child =
            Team.Create(
                "DeskSharing",
                root.Id);

        dbContext.Teams.AddRange(
            root,
            child);

        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        var persistedRoot =
            await dbContext.Teams
                .AsNoTracking()
                .SingleAsync(
                    team => team.Id == root.Id);

        var persistedChild =
            await dbContext.Teams
                .AsNoTracking()
                .SingleAsync(
                    team => team.Id == child.Id);

        Assert.Null(
            persistedRoot.ParentTeamId);

        Assert.Equal(
            root.Id,
            persistedChild.ParentTeamId);
    }
}
