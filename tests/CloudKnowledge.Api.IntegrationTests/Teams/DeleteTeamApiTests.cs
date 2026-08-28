using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Storage.Blobs;
using CloudKnowledge.Api.Contracts.Documents;
using CloudKnowledge.Api.Contracts.Teams;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Teams;

public sealed class DeleteTeamApiTests
{
    [Fact]
    public async Task Delete_ShouldMapChildrenAndRoleAuthorizationStatuses()
    {
        await using var postgres = CreatePostgres(
            "cloudknowledge_team_delete_api_status_test");

        await postgres.StartAsync();

        using var factory = CreateFactory(
            postgres.GetConnectionString(),
            "UseDevelopmentStorage=true");

        using var client = CreateClient(factory);

        await ApplyMigrationsAsync(factory);

        var parent = await CreateTeamAsync(
            client,
            "Parent");

        var child = await CreateTeamAsync(
            client,
            "Child",
            parent.Id);

        var parentDelete = await client.DeleteAsync(
            $"/api/teams/{parent.Id}");

        Assert.Equal(
            HttpStatusCode.Conflict,
            parentDelete.StatusCode);

        var childDelete = await client.DeleteAsync(
            $"/api/teams/{child.Id}");

        Assert.Equal(
            HttpStatusCode.NoContent,
            childDelete.StatusCode);

        var adminTeam = await CreateTeamAsync(
            client,
            "Admin Team");

        await ReplaceCurrentUserRoleAsync(
            factory,
            adminTeam.Id,
            TeamRole.Admin);

        var adminDelete = await client.DeleteAsync(
            $"/api/teams/{adminTeam.Id}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            adminDelete.StatusCode);

        var hiddenTeam = await CreateTeamAsync(
            client,
            "Hidden Team");

        await RemoveCurrentUserMembershipAsync(
            factory,
            hiddenTeam.Id);

        var hiddenDelete = await client.DeleteAsync(
            $"/api/teams/{hiddenTeam.Id}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            hiddenDelete.StatusCode);
    }

    [Fact]
    public async Task DeleteLeaf_ShouldDeleteTeamOwnedDocumentAndBlobButPreserveUserOwnedDocument()
    {
        await using var postgres = CreatePostgres(
            "cloudknowledge_team_delete_api_lifecycle_test");

        await using var azurite =
            new AzuriteBuilder(
                "mcr.microsoft.com/azure-storage/azurite:3.36.0")
                .Build();

        await postgres.StartAsync();
        await azurite.StartAsync();

        using var factory = CreateFactory(
            postgres.GetConnectionString(),
            azurite.GetConnectionString());

        using var client = CreateClient(factory);

        await ApplyMigrationsAsync(factory);

        var sourceTeam = await CreateTeamAsync(
            client,
            "Source Team");

        var preservedTeam = await CreateTeamAsync(
            client,
            "Preserved Team");

        var teamOwned = await UploadPdfAsync(
            client,
            "team-owned.pdf",
            sourceTeam.Id);

        var personal = await UploadPdfAsync(
            client,
            "personal.pdf");

        Assert.False(teamOwned.IsOwner);
        Assert.True(personal.IsOwner);

        var shareSourceResponse = await client.PutAsync(
            $"/api/documents/{personal.Id}/teams/{sourceTeam.Id}",
            content: null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            shareSourceResponse.StatusCode);

        var sharePreservedResponse = await client.PutAsync(
            $"/api/documents/{personal.Id}/teams/{preservedTeam.Id}",
            content: null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            sharePreservedResponse.StatusCode);

        var blobContainer = CreateBlobContainer(
            azurite.GetConnectionString());

        Assert.True(
            (await blobContainer
                .GetBlobClient(teamOwned.Id.ToString())
                .ExistsAsync()).Value);

        Assert.True(
            (await blobContainer
                .GetBlobClient(personal.Id.ToString())
                .ExistsAsync()).Value);

        var deleteResponse = await client.DeleteAsync(
            $"/api/teams/{sourceTeam.Id}");

        Assert.Equal(
            HttpStatusCode.NoContent,
            deleteResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CloudKnowledgeDbContext>();

            Assert.False(
                await dbContext.Teams.AsNoTracking().AnyAsync(
                    team => team.Id == sourceTeam.Id));

            Assert.False(
                await dbContext.Documents.AsNoTracking().AnyAsync(
                    document => document.Id == teamOwned.Id));

            Assert.True(
                await dbContext.Documents.AsNoTracking().AnyAsync(
                    document =>
                        document.Id == personal.Id &&
                        document.OwnerUserId.HasValue &&
                        document.OwnerTeamId == null));

            Assert.False(
                await dbContext.DocumentTeamAccess.AsNoTracking().AnyAsync(
                    access =>
                        access.DocumentId == personal.Id &&
                        access.TeamId == sourceTeam.Id));

            Assert.True(
                await dbContext.DocumentTeamAccess.AsNoTracking().AnyAsync(
                    access =>
                        access.DocumentId == personal.Id &&
                        access.TeamId == preservedTeam.Id));
        }

        Assert.False(
            (await blobContainer
                .GetBlobClient(teamOwned.Id.ToString())
                .ExistsAsync()).Value);

        Assert.True(
            (await blobContainer
                .GetBlobClient(personal.Id.ToString())
                .ExistsAsync()).Value);

        var personalDownload = await client.GetAsync(
            $"/api/documents/{personal.Id}/download");

        Assert.Equal(
            HttpStatusCode.OK,
            personalDownload.StatusCode);

        var deletedTeamDocument = await client.GetAsync(
            $"/api/documents/{teamOwned.Id}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            deletedTeamDocument.StatusCode);
    }

    private static PostgreSqlContainer CreatePostgres(
        string database)
    {
        return new PostgreSqlBuilder(
            "pgvector/pgvector:0.8.6-pg18")
            .WithDatabase(database)
            .WithUsername("cloudknowledge")
            .WithPassword("cloudknowledge_test")
            .Build();
    }

    private static CloudKnowledgeApiFactory CreateFactory(
        string postgresConnectionString,
        string storageConnectionString)
    {
        return new CloudKnowledgeApiFactory(
            postgresConnectionString,
            storageConnectionString);
    }

    private static HttpClient CreateClient(
        CloudKnowledgeApiFactory factory)
    {
        return factory.CreateClient(
            new()
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    private static async Task<TeamResponse> CreateTeamAsync(
        HttpClient client,
        string name,
        Guid? parentTeamId = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/teams",
            new
            {
                name,
                parentTeamId
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var team = await response.Content
            .ReadFromJsonAsync<TeamResponse>();

        Assert.NotNull(team);
        return team;
    }

    private static async Task<DocumentResponse> UploadPdfAsync(
        HttpClient client,
        string fileName,
        Guid? teamId = null)
    {
        using var multipart =
            new MultipartFormDataContent();

        using var file =
            new ByteArrayContent(
                new byte[] { 1, 2, 3, 4 });

        file.Headers.ContentType =
            new MediaTypeHeaderValue(
                "application/pdf");

        multipart.Add(
            file,
            "File",
            fileName);

        if (teamId.HasValue)
        {
            multipart.Add(
                new StringContent(
                    teamId.Value.ToString()),
                "TeamId");
        }

        var response = await client.PostAsync(
            "/api/documents",
            multipart);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var document = await response.Content
            .ReadFromJsonAsync<DocumentResponse>();

        Assert.NotNull(document);
        return document;
    }

    private static BlobContainerClient CreateBlobContainer(
        string storageConnectionString)
    {
        return new BlobContainerClient(
            storageConnectionString,
            "documents",
            new BlobClientOptions(
                BlobClientOptions.ServiceVersion.V2025_11_05));
    }

    private static async Task ReplaceCurrentUserRoleAsync(
        CloudKnowledgeApiFactory factory,
        Guid teamId,
        TeamRole role)
    {
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CloudKnowledgeDbContext>();

        var membership = await dbContext.TeamMembers
            .SingleAsync(member => member.TeamId == teamId);

        var userId = membership.UserId;

        dbContext.TeamMembers.Remove(membership);
        await dbContext.SaveChangesAsync();

        dbContext.TeamMembers.Add(
            TeamMember.Create(
                teamId,
                userId,
                role));

        await dbContext.SaveChangesAsync();
    }

    private static async Task RemoveCurrentUserMembershipAsync(
        CloudKnowledgeApiFactory factory,
        Guid teamId)
    {
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CloudKnowledgeDbContext>();

        var membership = await dbContext.TeamMembers
            .SingleAsync(member => member.TeamId == teamId);

        dbContext.TeamMembers.Remove(membership);
        await dbContext.SaveChangesAsync();
    }

    private static async Task ApplyMigrationsAsync(
        CloudKnowledgeApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CloudKnowledgeDbContext>();

        await dbContext.Database.MigrateAsync();
    }
}
