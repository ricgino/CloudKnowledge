using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Storage.Blobs;
using CloudKnowledge.Api.Contracts.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Azurite;
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

        await using var azurite =
            new AzuriteBuilder(
                "mcr.microsoft.com/azure-storage/azurite:3.36.0")
                .Build();

        await postgres.StartAsync();
        await azurite.StartAsync();

        using var factory =
            new CloudKnowledgeApiFactory(
                postgres.GetConnectionString(),
                azurite.GetConnectionString());

        using var client = factory.CreateClient(
            new()
            {
                BaseAddress = new Uri("https://localhost")
            });

        await ApplyMigrationsAsync(factory);

        var fileBytes =
            new byte[] { 1, 2, 3, 4, 5 };

        using var multipartContent =
            new MultipartFormDataContent();

        using var fileContent =
            new ByteArrayContent(fileBytes);

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(
                "application/pdf");

        multipartContent.Add(
            fileContent,
            "File",
            "integration-test.pdf");

        var createResponse =
            await client.PostAsync(
                "/api/documents",
                multipartContent);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var createdDocument =
            await createResponse.Content
                .ReadFromJsonAsync<DocumentResponse>();

        Assert.NotNull(createdDocument);

        Assert.NotEqual(
            Guid.Empty,
            createdDocument.Id);
        
        Assert.Equal(
            createdDocument.Id,
            factory.ProcessingQueue.PublishedDocumentId);

        Assert.Equal(
            "Pending",
            createdDocument.Status);

        var blobClientOptions =
            new BlobClientOptions(
                BlobClientOptions.ServiceVersion.V2025_11_05);

        var blobContainer =
            new BlobContainerClient(
                azurite.GetConnectionString(),
                "documents",
                blobClientOptions);

        var blobClient =
            blobContainer.GetBlobClient(
                createdDocument.Id.ToString());

        var blobExists =
            await blobClient.ExistsAsync();

        Assert.True(blobExists.Value);

        var getResponse =
            await client.GetAsync(
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

        Assert.Equal(
            createdDocument.Id,
            factory.ProcessingQueue.PublishedDocumentId);

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