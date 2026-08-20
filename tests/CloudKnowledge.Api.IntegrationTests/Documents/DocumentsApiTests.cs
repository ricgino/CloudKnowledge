using System.Net;
using System.Net.Http.Json;
using CloudKnowledge.Api.Contracts.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class DocumentsApiTests
{
    [Fact]
    public async Task CreateAndGetDocument_ShouldPersistDocument()
    {
        await using var postgres =
            new PostgreSqlBuilder("postgres:18")
                .WithDatabase("cloudknowledge_test")
                .WithUsername("cloudknowledge")
                .WithPassword("cloudknowledge_test")
                .Build();

        await postgres.StartAsync();

        using var factory =
            new CloudKnowledgeApiFactory(
                postgres.GetConnectionString());

        using var client = factory.CreateClient(
            new()
            {
                BaseAddress = new Uri("https://localhost")
            });

        await ApplyMigrationsAsync(factory);

        var createResponse = await client.PostAsJsonAsync(
            "/api/documents",
            new
            {
                fileName = "integration-test.pdf",
                contentType = "application/pdf"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var createdDocument =
            await createResponse.Content
                .ReadFromJsonAsync<DocumentResponse>();

        Assert.NotNull(createdDocument);
        Assert.NotEqual(Guid.Empty, createdDocument.Id);
        Assert.Equal("Pending", createdDocument.Status);

        var getResponse = await client.GetAsync(
            $"/api/documents/{createdDocument.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        var retrievedDocument =
            await getResponse.Content
                .ReadFromJsonAsync<DocumentResponse>();

        Assert.NotNull(retrievedDocument);

        Assert.Equal(
            createdDocument.Id,
            retrievedDocument.Id);

        Assert.Equal(
            "integration-test.pdf",
            retrievedDocument.FileName);
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