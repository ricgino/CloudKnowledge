using System.Net;
using System.Net.Http.Json;
using CloudKnowledge.Api.Contracts.Teams;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class DocumentRetryApiTests
{
    [Fact]
    public async Task Retry_WhenCurrentUserOwnsFailedDocument_ShouldRequeueProcessing()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase("cloudknowledge_document_retry_api_test")
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
                "Retry Test Team");

        Guid documentId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<CloudKnowledgeDbContext>();

            var currentUserId =
                await dbContext.TeamMembers
                    .Where(
                        membership =>
                            membership.TeamId == team.Id)
                    .Select(
                        membership => membership.UserId)
                    .SingleAsync();

            var document =
                Document.Create(
                    "failed-owned.txt",
                    "text/plain");

            document.AssignUserOwner(
                currentUserId);
            document.MarkAsProcessing();
            document.MarkAsFailed();

            documentId = document.Id;

            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync();
        }

        Assert.Null(
            factory.ProcessingQueue.PublishedDocumentId);

        var response =
            await client.PostAsync(
                $"/api/documents/{documentId}/retry",
                content: null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<CloudKnowledgeDbContext>();

            var status =
                await dbContext.Documents
                    .Where(document => document.Id == documentId)
                    .Select(document => document.Status)
                    .SingleAsync();

            Assert.Equal(
                DocumentStatus.Pending,
                status);
        }

        Assert.Equal(
            documentId,
            factory.ProcessingQueue.PublishedDocumentId);
    }

    [Fact]
    public async Task Retry_WhenCurrentUserDoesNotOwnDocument_ShouldReturnNotFoundWithoutRequeueing()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase("cloudknowledge_document_retry_owner_api_test")
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

        await CreateTeamAsync(
            client,
            "Retry Owner Guard Team");

        Guid documentId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<CloudKnowledgeDbContext>();

            var otherUser =
                UserAccount.Create(
                    "other.retry.owner@example.com",
                    "Other Retry Owner");

            dbContext.UserAccounts.Add(otherUser);

            var document =
                Document.Create(
                    "failed-other-owner.txt",
                    "text/plain");

            document.AssignUserOwner(
                otherUser.Id);
            document.MarkAsProcessing();
            document.MarkAsFailed();

            documentId = document.Id;

            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync();
        }

        Assert.Null(
            factory.ProcessingQueue.PublishedDocumentId);

        var response =
            await client.PostAsync(
                $"/api/documents/{documentId}/retry",
                content: null);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<CloudKnowledgeDbContext>();

            var status =
                await dbContext.Documents
                    .Where(document => document.Id == documentId)
                    .Select(document => document.Status)
                    .SingleAsync();

            Assert.Equal(
                DocumentStatus.Failed,
                status);
        }

        Assert.Null(
            factory.ProcessingQueue.PublishedDocumentId);
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
