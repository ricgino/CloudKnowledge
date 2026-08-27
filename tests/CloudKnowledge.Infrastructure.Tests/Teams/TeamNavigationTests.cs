using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Infrastructure.Tests.Teams;

public sealed class TeamNavigationTests
{
    [Fact]
    public async Task GetForUserAsync_ShouldIncludeStructuralAncestorsOnly()
    {
        await using var postgres =
            CreatePostgres();

        await postgres.StartAsync();

        await using var dbContext =
            await CreateDbContextAsync(
                postgres.GetConnectionString());

        var stellantis =
            Team.Create(
                "Stellantis");

        var finance =
            Team.Create(
                "Finance",
                stellantis.Id);

        var reporting =
            Team.Create(
                "Reporting",
                finance.Id);

        var budgeting =
            Team.Create(
                "Budgeting",
                finance.Id);

        var userId =
            Guid.NewGuid();

        dbContext.Teams.AddRange(
            stellantis,
            finance,
            reporting,
            budgeting);

        dbContext.TeamMembers.Add(
            TeamMember.Create(
                reporting.Id,
                userId,
                TeamRole.Member));

        await dbContext.SaveChangesAsync();

        var repository =
            new EfTeamRepository(
                dbContext);

        var results =
            await repository.GetForUserAsync(
                userId,
                CancellationToken.None);

        Assert.Equal(
            3,
            results.Count);

        Assert.DoesNotContain(
            results,
            result => result.Id == budgeting.Id);

        var root =
            Assert.Single(
                results,
                result => result.Id == stellantis.Id);

        Assert.Null(
            root.ParentTeamId);
        Assert.False(
            root.IsMember);
        Assert.Null(
            root.Role);
        Assert.False(
            root.CanManage);

        var structuralParent =
            Assert.Single(
                results,
                result => result.Id == finance.Id);

        Assert.Equal(
            stellantis.Id,
            structuralParent.ParentTeamId);
        Assert.False(
            structuralParent.IsMember);
        Assert.Null(
            structuralParent.Role);
        Assert.False(
            structuralParent.CanManage);

        var memberTeam =
            Assert.Single(
                results,
                result => result.Id == reporting.Id);

        Assert.Equal(
            finance.Id,
            memberTeam.ParentTeamId);
        Assert.True(
            memberTeam.IsMember);
        Assert.Equal(
            TeamRole.Member,
            memberTeam.Role);
        Assert.False(
            memberTeam.CanManage);
    }

    [Theory]
    [InlineData(TeamRole.Admin)]
    [InlineData(TeamRole.Owner)]
    public async Task GetForUserAsync_ShouldMarkDirectManagersAsManageable(
        TeamRole role)
    {
        await using var postgres =
            CreatePostgres();

        await postgres.StartAsync();

        await using var dbContext =
            await CreateDbContextAsync(
                postgres.GetConnectionString());

        var team =
            Team.Create(
                "Rai");

        var userId =
            Guid.NewGuid();

        dbContext.Teams.Add(
            team);

        dbContext.TeamMembers.Add(
            TeamMember.Create(
                team.Id,
                userId,
                role));

        await dbContext.SaveChangesAsync();

        var repository =
            new EfTeamRepository(
                dbContext);

        var result =
            Assert.Single(
                await repository.GetForUserAsync(
                    userId,
                    CancellationToken.None));

        Assert.True(
            result.IsMember);
        Assert.Equal(
            role,
            result.Role);
        Assert.True(
            result.CanManage);
    }

    private static PostgreSqlContainer CreatePostgres()
    {
        return new PostgreSqlBuilder(
            "pgvector/pgvector:0.8.6-pg18")
            .WithDatabase(
                "cloudknowledge_team_navigation_test")
            .WithUsername(
                "cloudknowledge")
            .WithPassword(
                "cloudknowledge_test")
            .Build();
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

        await dbContext.Database
            .MigrateAsync();

        return dbContext;
    }
}
