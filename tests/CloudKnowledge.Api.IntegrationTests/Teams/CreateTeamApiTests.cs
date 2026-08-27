using System.Net;
using System.Net.Http.Json;
using CloudKnowledge.Api.Contracts.Teams;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Teams;

public sealed class CreateTeamApiTests
{
    [Fact]
    public async Task CreateChild_ShouldPersistParentForParentOwner()
    {
        await using var postgres =
            CreatePostgres();

        await postgres.StartAsync();

        using var factory =
            CreateFactory(
                postgres.GetConnectionString());

        using var client =
            factory.CreateClient(
                new()
                {
                    BaseAddress =
                        new Uri("https://localhost")
                });

        await ApplyMigrationsAsync(
            factory);

        var rootResponse =
            await client.PostAsJsonAsync(
                "/api/teams",
                new
                {
                    name = "Rai"
                });

        Assert.Equal(
            HttpStatusCode.Created,
            rootResponse.StatusCode);

        var root =
            await rootResponse.Content
                .ReadFromJsonAsync<TeamResponse>();

        Assert.NotNull(
            root);

        var childResponse =
            await client.PostAsJsonAsync(
                "/api/teams",
                new
                {
                    name = "DeskSharing",
                    parentTeamId = root.Id
                });

        Assert.Equal(
            HttpStatusCode.Created,
            childResponse.StatusCode);

        var child =
            await childResponse.Content
                .ReadFromJsonAsync<TeamResponse>();

        Assert.NotNull(
            child);

        using var scope =
            factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<CloudKnowledgeDbContext>();

        var persistedChild =
            await dbContext.Teams
                .AsNoTracking()
                .SingleAsync(
                    team => team.Id == child.Id);

        Assert.Equal(
            root.Id,
            persistedChild.ParentTeamId);

        var childMemberships =
            await dbContext.TeamMembers
                .AsNoTracking()
                .Where(
                    membership =>
                        membership.TeamId == child.Id)
                .ToListAsync();

        Assert.Single(
            childMemberships);

        Assert.Equal(
            TeamRole.Owner,
            childMemberships[0].Role);
    }

    [Fact]
    public async Task CreateChild_ShouldReturnForbiddenForParentMember()
    {
        await using var postgres =
            CreatePostgres();

        await postgres.StartAsync();

        using var factory =
            CreateFactory(
                postgres.GetConnectionString());

        using var client =
            factory.CreateClient(
                new()
                {
                    BaseAddress =
                        new Uri("https://localhost")
                });

        await ApplyMigrationsAsync(
            factory);

        var rootResponse =
            await client.PostAsJsonAsync(
                "/api/teams",
                new
                {
                    name = "Rai"
                });

        Assert.Equal(
            HttpStatusCode.Created,
            rootResponse.StatusCode);

        var root =
            await rootResponse.Content
                .ReadFromJsonAsync<TeamResponse>();

        Assert.NotNull(
            root);

        using (var scope =
            factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<CloudKnowledgeDbContext>();

            var ownerMembership =
                await dbContext.TeamMembers
                    .SingleAsync(
                        membership =>
                            membership.TeamId == root.Id);

            var userId =
                ownerMembership.UserId;

            dbContext.TeamMembers.Remove(
                ownerMembership);

            await dbContext.SaveChangesAsync();

            dbContext.TeamMembers.Add(
                TeamMember.Create(
                    root.Id,
                    userId,
                    TeamRole.Member));

            await dbContext.SaveChangesAsync();
        }

        var childResponse =
            await client.PostAsJsonAsync(
                "/api/teams",
                new
                {
                    name = "DeskSharing",
                    parentTeamId = root.Id
                });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            childResponse.StatusCode);
    }

    private static PostgreSqlContainer CreatePostgres()
    {
        return new PostgreSqlBuilder(
            "pgvector/pgvector:0.8.6-pg18")
            .WithDatabase(
                "cloudknowledge_team_creation_test")
            .WithUsername(
                "cloudknowledge")
            .WithPassword(
                "cloudknowledge_test")
            .Build();
    }

    private static CloudKnowledgeApiFactory CreateFactory(
        string postgresConnectionString)
    {
        return new CloudKnowledgeApiFactory(
            postgresConnectionString,
            "UseDevelopmentStorage=true");
    }

    private static async Task ApplyMigrationsAsync(
        CloudKnowledgeApiFactory factory)
    {
        using var scope =
            factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<CloudKnowledgeDbContext>();

        await dbContext.Database
            .MigrateAsync();
    }
}
