using System.Net;
using System.Net.Http.Json;
using CloudKnowledge.Api.Contracts.Documents;
using CloudKnowledge.Api.Contracts.Teams;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class TeamMemberDocumentVisibilityApiTests
{
    [Fact]
    public async Task DirectMember_ShouldSeeTeamOwnedDocument_InAllAndTeamScopes()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase("cloudknowledge_team_member_visibility_test")
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
                    BaseAddress = new Uri("https://localhost")
                });

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<CloudKnowledgeDbContext>();

            await dbContext.Database.MigrateAsync();
        }

        var createTeamResponse =
            await client.PostAsJsonAsync(
                "/api/teams",
                new
                {
                    name = "Engineering",
                    parentTeamId = (Guid?)null
                });

        Assert.Equal(
            HttpStatusCode.Created,
            createTeamResponse.StatusCode);

        var team =
            Assert.IsType<TeamResponse>(
                await createTeamResponse.Content
                    .ReadFromJsonAsync<TeamResponse>());

        Guid currentUserId;
        Guid documentId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<CloudKnowledgeDbContext>();

            var ownerMembership =
                await dbContext.TeamMembers
                    .SingleAsync(
                        membership =>
                            membership.TeamId == team.Id);

            currentUserId =
                ownerMembership.UserId;

            dbContext.TeamMembers.Remove(
                ownerMembership);

            dbContext.TeamMembers.Add(
                TeamMember.Create(
                    team.Id,
                    currentUserId,
                    TeamRole.Member));

            var document =
                Document.Create(
                    "team-member-visible.pdf",
                    "application/pdf");

            document.AssignTeamOwner(
                team.Id);

            documentId =
                document.Id;

            dbContext.Documents.Add(
                document);

            await dbContext.SaveChangesAsync();
        }

        var teamsResponse =
            await client.GetFromJsonAsync<TeamResponse[]>(
                "/api/teams");

        var currentTeam =
            Assert.Single(
                Assert.IsType<TeamResponse[]>(
                    teamsResponse));

        Assert.True(
            currentTeam.IsMember);
        Assert.Equal(
            "Member",
            currentTeam.Role);
        Assert.False(
            currentTeam.CanManage);

        await AssertDocumentVisibleAsync(
            client,
            "/api/documents?scope=all",
            documentId);

        await AssertDocumentVisibleAsync(
            client,
            $"/api/documents?scope=team&teamId={team.Id}",
            documentId);
    }

    private static async Task AssertDocumentVisibleAsync(
        HttpClient client,
        string requestUri,
        Guid expectedDocumentId)
    {
        var response =
            await client.GetAsync(
                requestUri);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var documents =
            Assert.IsType<GetDocumentsResponse>(
                await response.Content
                    .ReadFromJsonAsync<GetDocumentsResponse>());

        var document =
            Assert.Single(
                documents.Items,
                item =>
                    item.Id == expectedDocumentId);

        Assert.Equal(
            "team-member-visible.pdf",
            document.FileName);
        Assert.False(
            document.IsOwner);
        Assert.False(
            document.CanDelete);
        Assert.Contains(
            document.SharedTeams,
            sharedTeam =>
                sharedTeam.Id ==
                Assert.Single(
                    documents.Items,
                    item => item.Id == expectedDocumentId)
                    .SharedTeams.Single().Id);
    }
}
