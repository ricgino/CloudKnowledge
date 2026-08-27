using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CloudKnowledge.Api.Contracts.Teams;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class DocumentLibraryApiTests
{
    [Fact]
    public async Task GetDocuments_ShouldApplyAuthorizedScopesSearchAndValidation()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase("cloudknowledge_document_library_api_test")
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

        await ApplyMigrationsAsync(
            factory);

        var rai =
            await CreateTeamAsync(
                client,
                "Rai");

        var deskSharing =
            await CreateTeamAsync(
                client,
                "DeskSharing",
                rai.Id);

        var hrPortal =
            await CreateTeamAsync(
                client,
                "HR Portal",
                rai.Id);

        Guid currentUserId;
        Guid deskDocumentId;

        using (var scope =
            factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<CloudKnowledgeDbContext>();

            currentUserId =
                await dbContext.TeamMembers
                    .Where(
                        membership =>
                            membership.TeamId == deskSharing.Id)
                    .Select(
                        membership => membership.UserId)
                    .SingleAsync();

            var rootMembership =
                await dbContext.TeamMembers
                    .SingleAsync(
                        membership =>
                            membership.TeamId == rai.Id
                            && membership.UserId == currentUserId);

            dbContext.TeamMembers.Remove(
                rootMembership);

            var booking =
                Team.Create(
                    "Booking",
                    rai.Id);

            var otherUser =
                UserAccount.Create(
                    "api.document.owner@example.com",
                    "API Document Owner");

            dbContext.Teams.Add(
                booking);

            dbContext.UserAccounts.Add(
                otherUser);

            var mine =
                CreateOwnedDocument(
                    "mine-architecture.pdf",
                    currentUserId);

            var desk =
                CreateOwnedDocument(
                    "desk-manuale.pdf",
                    otherUser.Id);

            var hr =
                CreateOwnedDocument(
                    "HR-Handbook.PDF",
                    otherUser.Id);

            var bookingSecret =
                CreateOwnedDocument(
                    "booking-secret.pdf",
                    otherUser.Id);

            deskDocumentId =
                desk.Id;

            dbContext.Documents.AddRange(
                mine,
                desk,
                hr,
                bookingSecret);

            await dbContext.SaveChangesAsync();

            dbContext.DocumentTeamAccess.AddRange(
                DocumentTeamAccess.Create(
                    desk.Id,
                    deskSharing.Id),
                DocumentTeamAccess.Create(
                    hr.Id,
                    hrPortal.Id),
                DocumentTeamAccess.Create(
                    bookingSecret.Id,
                    booking.Id));

            await dbContext.SaveChangesAsync();
        }

        await AssertFilesAsync(
            client,
            "/api/documents?scope=all",
            HttpStatusCode.OK,
            "mine-architecture.pdf",
            "desk-manuale.pdf",
            "HR-Handbook.PDF");

        await AssertFilesAsync(
            client,
            "/api/documents?scope=owned",
            HttpStatusCode.OK,
            "mine-architecture.pdf");

        await AssertFilesAsync(
            client,
            $"/api/documents?scope=team&teamId={deskSharing.Id}",
            HttpStatusCode.OK,
            "desk-manuale.pdf");

        await AssertFilesAsync(
            client,
            $"/api/documents?scope=team&teamId={rai.Id}&includeDescendants=true",
            HttpStatusCode.OK,
            "desk-manuale.pdf",
            "HR-Handbook.PDF");

        await AssertFilesAsync(
            client,
            "/api/documents?scope=all&query=handbook",
            HttpStatusCode.OK,
            "HR-Handbook.PDF");

        var deskResponse =
            await client.GetAsync(
                $"/api/documents?scope=team&teamId={deskSharing.Id}");

        var json =
            JsonDocument.Parse(
                await deskResponse.Content.ReadAsStringAsync());

        var deskItem =
            json.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .Single(
                    item =>
                        item.GetProperty("id").GetGuid() == deskDocumentId);

        var sharedTeams =
            deskItem.GetProperty("sharedTeams");

        Assert.Equal(
            1,
            sharedTeams.GetArrayLength());

        Assert.Equal(
            "Rai / DeskSharing",
            sharedTeams[0]
                .GetProperty("path")
                .GetString());

        var invalidRequests =
            new[]
            {
                "/api/documents?scope=team",
                $"/api/documents?scope=owned&teamId={deskSharing.Id}",
                "/api/documents?scope=owned&includeDescendants=true",
                "/api/documents?scope=unknown"
            };

        foreach (var requestUri in invalidRequests)
        {
            var response =
                await client.GetAsync(
                    requestUri);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                response.StatusCode);
        }
    }

    private static async Task<TeamResponse> CreateTeamAsync(
        HttpClient client,
        string name,
        Guid? parentTeamId = null)
    {
        var response =
            await client.PostAsJsonAsync(
                "/api/teams",
                new
                {
                    name,
                    parentTeamId
                });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        return (
            await response.Content
                .ReadFromJsonAsync<TeamResponse>())!;
    }

    private static async Task AssertFilesAsync(
        HttpClient client,
        string requestUri,
        HttpStatusCode expectedStatus,
        params string[] expectedFileNames)
    {
        var response =
            await client.GetAsync(
                requestUri);

        Assert.Equal(
            expectedStatus,
            response.StatusCode);

        var json =
            JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

        var actualFileNames =
            json.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .Select(
                    item =>
                        item.GetProperty("fileName")
                            .GetString())
                .Where(
                    fileName => fileName is not null)
                .Select(
                    fileName => fileName!)
                .OrderBy(
                    fileName => fileName)
                .ToArray();

        Assert.Equal(
            expectedFileNames
                .OrderBy(fileName => fileName)
                .ToArray(),
            actualFileNames);
    }

    private static Document CreateOwnedDocument(
        string fileName,
        Guid ownerUserId)
    {
        var document =
            Document.Create(
                fileName,
                "application/pdf");

        document.AssignOwner(
            ownerUserId);

        return document;
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
