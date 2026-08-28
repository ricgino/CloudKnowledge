using System.Net;
using System.Net.Http.Headers;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class DocumentFormatUploadApiTests
{
    [Fact]
    public async Task Upload_ShouldAcceptPdfDocxAndTxt_AndRejectUnsupportedExtensions()
    {
        await using var postgres =
            new PostgreSqlBuilder("pgvector/pgvector:0.8.6-pg18")
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

        var supportedFiles =
            new[]
            {
                ("architecture.pdf", "application/pdf"),
                (
                    "handbook.docx",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
                ("notes.txt", "text/plain")
            };

        foreach (var (fileName, contentType) in supportedFiles)
        {
            using var content =
                CreateMultipart(
                    fileName,
                    contentType);

            var response =
                await client.PostAsync(
                    "/api/documents",
                    content);

            Assert.Equal(
                HttpStatusCode.Created,
                response.StatusCode);
        }

        using var unsupportedContent =
            CreateMultipart(
                "payload.exe",
                "application/octet-stream");

        var unsupportedResponse =
            await client.PostAsync(
                "/api/documents",
                unsupportedContent);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            unsupportedResponse.StatusCode);
    }

    private static MultipartFormDataContent CreateMultipart(
        string fileName,
        string contentType)
    {
        var multipart =
            new MultipartFormDataContent();

        var file =
            new ByteArrayContent(
                new byte[] { 1, 2, 3, 4, 5 });

        file.Headers.ContentType =
            new MediaTypeHeaderValue(contentType);

        multipart.Add(
            file,
            "File",
            fileName);

        return multipart;
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
