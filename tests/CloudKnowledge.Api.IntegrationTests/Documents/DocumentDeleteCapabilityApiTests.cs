using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CloudKnowledge.Api.Contracts.Teams;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class DocumentDeleteCapabilityApiTests
{
    [Fact]
    public async Task GetDocuments_ShouldExposeCanDeleteWithoutChangingPersonalOwnership()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase("cloudknowledge_document_delete_capability_api_test")
                .WithUsername("cloudknowledge")
                .WithPassword("cloudknowledge_test")
                .Build();

        await postgres.StartAsync();

        using var factory =
            new CloudKnowledgeApiFactory(
                postgres.GetConnectionString(),
                "UseDevelopmentStorage=true");

        using var client =
            factory.CreateClient(
                new()
                {
                    BaseAddress =
                        new Uri("https://localhost")
                });

        await ApplyMigrationsAsync(factory);

        var team =
            await CreateTeamAsync(
                client,
                "Dota");

        Guid currentUserId;
        Guid teamOwnedDocumentId;
        Guid sharedDocumentId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<CloudKnowledgeDbContext>();

            currentUserId =
                await dbContext.TeamMembers
                    .Where(
                        membership =>
                            membership.TeamId == team.Id)
                    .Select(
                        membership => membership.UserId)
                    .SingleAsync();

            var otherUser =
                UserAccount.Create(
                    "other.document.owner@example.com",
                    "Other Document Owner");

            dbContext.UserAccounts.Add(otherUser);

            var personal =
                Document.Create(
                    "personal.pdf",
                    "application/pdf");
            personal.AssignUserOwner(currentUserId);

            var teamOwned =
                Document.Create(
                    "team-owned.pdf",
                    "application/pdf");
            teamOwned.AssignTeamOwner(team.Id);
            teamOwnedDocumentId = teamOwned.Id;

            var shared =
                Document.Create(
                    "shared-only.pdf",
                    "application/pdf");
            shared.AssignUserOwner(otherUser.Id);
            sharedDocumentId = shared.Id;

            dbContext.Documents.AddRange(
                personal,
                teamOwned,
                shared);

            await dbContext.SaveChangesAsync();

            dbContext.DocumentTeamAccess.Add(
                DocumentTeamAccess.Create(
                    shared.Id,
                    team.Id));

            await dbContext.SaveChangesAsync();
        }

        var response =
            await client.GetAsync(
                $"/api/documents?scope=team&teamId={team.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var json =
            JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

        var items =
            json.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .ToArray();

        var teamOwnedItem =
            items.Single(
                item =>
                    item.GetProperty("id").GetGuid() ==
                    teamOwnedDocumentId);

        Assert.False(
            teamOwnedItem.GetProperty("isOwner")
                .GetBoolean());
        Assert.True(
            teamOwnedItem.GetProperty("canDelete")
                .GetBoolean());

        var sharedItem =
            items.Single(
                item =>
                    item.GetProperty("id").GetGuid() ==
                    sharedDocumentId);

        Assert.False(
            sharedItem.GetProperty("isOwner")
                .GetBoolean());
        Assert.False(
            sharedItem.GetProperty("canDelete")
                .GetBoolean());
    }

    private static async Task<TeamResponse> CreateTeamAsync(
        HttpClient client,
        string name)
    {
        var response =
            await client.PostAsJsonAsync(
                "/api/teams",
                new
                {
                    name
                });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        return (
            await response.Content
                .ReadFromJsonAsync<TeamResponse>())!;
    }

    private static async Task ApplyMigrationsAsync(
        CloudKnowledgeApiFactory factory)
    {
        using var scope =
            factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<CloudKnowledgeDbContext>();

        await dbContext.Database.MigrateAsync();
    }
}
