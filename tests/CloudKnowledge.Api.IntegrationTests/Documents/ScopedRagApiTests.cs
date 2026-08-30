using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class ScopedRagApiTests
{
    [Fact]
    public async Task SearchAndAsk_ShouldValidateAndEnforceTeamScope()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase(
                    "cloudknowledge_scoped_rag_api_test")
                .WithUsername(
                    "cloudknowledge")
                .WithPassword(
                    "cloudknowledge_test")
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

        var createTeamResponse =
            await client.PostAsJsonAsync(
                "/api/teams",
                new
                {
                    name = "Engineering"
                });

        Assert.Equal(
            HttpStatusCode.Created,
            createTeamResponse.StatusCode);

        var createdTeam =
            JsonDocument.Parse(
                await createTeamResponse.Content.ReadAsStringAsync());

        var teamId =
            createdTeam.RootElement
                .GetProperty("id")
                .GetGuid();

        Guid personalDocumentId;
        Guid teamDocumentId;

        using (var scope =
            factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<CloudKnowledgeDbContext>();

            var currentUserId =
                await dbContext.TeamMembers
                    .Where(
                        membership =>
                            membership.TeamId == teamId)
                    .Select(
                        membership => membership.UserId)
                    .SingleAsync();

            var otherUser =
                UserAccount.Create(
                    "rag.owner@example.com",
                    "RAG Owner");

            var personalDocument =
                Document.Create(
                    "personal-not-in-team.pdf",
                    "application/pdf");

            personalDocument.AssignOwner(
                currentUserId);

            var teamDocument =
                Document.Create(
                    "engineering-handbook.pdf",
                    "application/pdf");

            teamDocument.AssignOwner(
                otherUser.Id);

            personalDocumentId =
                personalDocument.Id;

            teamDocumentId =
                teamDocument.Id;

            dbContext.AddRange(
                otherUser,
                personalDocument,
                teamDocument);

            await dbContext.SaveChangesAsync();

            dbContext.DocumentTeamAccess.Add(
                DocumentTeamAccess.Create(
                    teamDocument.Id,
                    teamId));

            var personalChunk =
                DocumentChunk.Create(
                    personalDocument.Id,
                    0,
                    "PERSONAL PRIVATE KNOWLEDGE");

            var teamChunk =
                DocumentChunk.Create(
                    teamDocument.Id,
                    0,
                    "ENGINEERING TEAM KNOWLEDGE");

            dbContext.DocumentChunks.AddRange(
                personalChunk,
                teamChunk);

            await dbContext.SaveChangesAsync();

            dbContext.DocumentChunkEmbeddings.AddRange(
                CreateEmbedding(
                    personalChunk,
                    firstComponent: 1.0f,
                    secondComponent: 0.0f),
                CreateEmbedding(
                    teamChunk,
                    firstComponent: 0.8f,
                    secondComponent: 0.2f));

            await dbContext.SaveChangesAsync();
        }

        var invalidPayloads =
            new object[]
            {
                new
                {
                    scope = "team",
                    teamId = (Guid?)null,
                    includeDescendants = false
                },
                new
                {
                    scope = "all",
                    teamId = (Guid?)teamId,
                    includeDescendants = false
                },
                new
                {
                    scope = "all",
                    teamId = (Guid?)null,
                    includeDescendants = true
                },
                new
                {
                    scope = "unknown",
                    teamId = (Guid?)null,
                    includeDescendants = false
                },
                new
                {
                    scope = "team",
                    teamId = (Guid?)Guid.Empty,
                    includeDescendants = false
                }
            };

        foreach (var invalid in invalidPayloads)
        {
            var invalidSearch =
                await client.PostAsJsonAsync(
                    "/api/search",
                    MergeSearchPayload(
                        invalid));

            Assert.Equal(
                HttpStatusCode.BadRequest,
                invalidSearch.StatusCode);

            var invalidAsk =
                await client.PostAsJsonAsync(
                    "/api/ask",
                    MergeAskPayload(
                        invalid));

            Assert.Equal(
                HttpStatusCode.BadRequest,
                invalidAsk.StatusCode);
        }

        var globalSearch =
            await client.PostAsJsonAsync(
                "/api/search",
                new
                {
                    query = "knowledge",
                    take = 5,
                    scope = "all"
                });

        Assert.Equal(
            HttpStatusCode.OK,
            globalSearch.StatusCode);

        var globalSearchDocumentIds =
            ReadSearchDocumentIds(
                await globalSearch.Content.ReadAsStringAsync());

        Assert.Contains(
            personalDocumentId,
            globalSearchDocumentIds);
        Assert.Contains(
            teamDocumentId,
            globalSearchDocumentIds);

        var scopedSearch =
            await client.PostAsJsonAsync(
                "/api/search",
                new
                {
                    query = "knowledge",
                    take = 5,
                    scope = "team",
                    teamId,
                    includeDescendants = false
                });

        Assert.Equal(
            HttpStatusCode.OK,
            scopedSearch.StatusCode);

        Assert.Equal(
            new[]
            {
                teamDocumentId
            },
            ReadSearchDocumentIds(
                await scopedSearch.Content.ReadAsStringAsync()));

        const string scopedQuestion =
            "What knowledge is available?";

        var scopedAsk =
            await client.PostAsJsonAsync(
                "/api/ask",
                new
                {
                    question = scopedQuestion,
                    take = 5,
                    scope = "team",
                    teamId,
                    includeDescendants = false
                });

        Assert.Equal(
            HttpStatusCode.OK,
            scopedAsk.StatusCode);

        using var askJson =
            JsonDocument.Parse(
                await scopedAsk.Content.ReadAsStringAsync());

        var askSourceDocumentIds =
            askJson.RootElement
                .GetProperty("sources")
                .EnumerateArray()
                .Select(
                    source =>
                        source.GetProperty("documentId")
                            .GetGuid())
                .ToArray();

        Assert.Equal(
            new[]
            {
                teamDocumentId
            },
            askSourceDocumentIds);

        Assert.True(
            askJson.RootElement.TryGetProperty(
                "retrievalQueries",
                out var retrievalQueries));

        Assert.Equal(
            scopedQuestion,
            retrievalQueries[0].GetString());

        var unauthorizedTeamId =
            Guid.NewGuid();

        var unauthorizedSearch =
            await client.PostAsJsonAsync(
                "/api/search",
                new
                {
                    query = "knowledge",
                    take = 5,
                    scope = "team",
                    teamId = unauthorizedTeamId,
                    includeDescendants = false
                });

        Assert.Equal(
            HttpStatusCode.OK,
            unauthorizedSearch.StatusCode);
        Assert.Empty(
            ReadSearchDocumentIds(
                await unauthorizedSearch.Content.ReadAsStringAsync()));

        var unauthorizedAsk =
            await client.PostAsJsonAsync(
                "/api/ask",
                new
                {
                    question = "What knowledge is available?",
                    take = 5,
                    scope = "team",
                    teamId = unauthorizedTeamId,
                    includeDescendants = false
                });

        Assert.Equal(
            HttpStatusCode.OK,
            unauthorizedAsk.StatusCode);

        using var unauthorizedAskJson =
            JsonDocument.Parse(
                await unauthorizedAsk.Content.ReadAsStringAsync());

        Assert.Empty(
            unauthorizedAskJson.RootElement
                .GetProperty("sources")
                .EnumerateArray());
    }

    private static object MergeSearchPayload(
        object scopePayload)
    {
        using var scopeJson =
            JsonDocument.Parse(
                JsonSerializer.Serialize(
                    scopePayload));

        var root =
            scopeJson.RootElement;

        return new
        {
            query = "knowledge",
            take = 5,
            scope = root.GetProperty("scope").GetString(),
            teamId = root.GetProperty("teamId").ValueKind == JsonValueKind.Null
                ? (Guid?)null
                : root.GetProperty("teamId").GetGuid(),
            includeDescendants = root.GetProperty("includeDescendants").GetBoolean()
        };
    }

    private static object MergeAskPayload(
        object scopePayload)
    {
        using var scopeJson =
            JsonDocument.Parse(
                JsonSerializer.Serialize(
                    scopePayload));

        var root =
            scopeJson.RootElement;

        return new
        {
            question = "What knowledge is available?",
            take = 5,
            scope = root.GetProperty("scope").GetString(),
            teamId = root.GetProperty("teamId").ValueKind == JsonValueKind.Null
                ? (Guid?)null
                : root.GetProperty("teamId").GetGuid(),
            includeDescendants = root.GetProperty("includeDescendants").GetBoolean()
        };
    }

    private static Guid[] ReadSearchDocumentIds(
        string json)
    {
        using var document =
            JsonDocument.Parse(
                json);

        return document.RootElement
            .EnumerateArray()
            .Select(
                item =>
                    item.GetProperty("documentId")
                        .GetGuid())
            .ToArray();
    }

    private static DocumentChunkEmbeddingRow CreateEmbedding(
        DocumentChunk chunk,
        float firstComponent,
        float secondComponent)
    {
        var vector =
            new float[768];

        vector[0] =
            firstComponent;
        vector[1] =
            secondComponent;

        return new DocumentChunkEmbeddingRow
        {
            ChunkId = chunk.Id,
            DocumentId = chunk.DocumentId,
            Embedding = new Vector(vector)
        };
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
