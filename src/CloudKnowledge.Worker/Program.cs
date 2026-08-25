using Azure.Messaging.ServiceBus;
using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.ProcessDocument;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Worker;
using Microsoft.EntityFrameworkCore;
using CloudKnowledge.Application.Documents.FailDocument;
using Azure.Storage.Blobs;
using Pgvector.EntityFrameworkCore;
using CloudKnowledge.Application.Documents.AskDocuments;

var builder =
    Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<CloudKnowledgeDbContext>(
    (serviceProvider, options) =>
    {
        var configuration =
            serviceProvider.GetRequiredService<IConfiguration>();

        var connectionString =
            configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' was not found.");

        options.UseNpgsql(
            connectionString,
            npgsqlOptions =>
                npgsqlOptions.UseVector());
    });

builder.Services.AddSingleton(
    serviceProvider =>
    {
        var configuration =
            serviceProvider.GetRequiredService<IConfiguration>();

        var connectionString =
            configuration["Storage:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Storage connection string was not found.");

        var containerName =
            configuration["Storage:ContainerName"]
            ?? throw new InvalidOperationException(
                "Storage container name was not found.");

        var blobClientOptions =
            new BlobClientOptions(
                BlobClientOptions.ServiceVersion.V2025_11_05);

        return new BlobContainerClient(
            connectionString,
            containerName,
            blobClientOptions);
    });

builder.Services.AddScoped<
    IDocumentStorage,
    AzureBlobDocumentStorage>();

builder.Services.AddScoped<
    IDocumentTextExtractor,
    PdfPigDocumentTextExtractor>();

builder.Services.AddScoped<
    IDocumentRepository,
    EfDocumentRepository>();

builder.Services.AddScoped<ProcessDocumentUseCase>();
builder.Services.AddScoped<FailDocumentUseCase>();

builder.Services.AddScoped<
    IDocumentChunkRepository,
    EfDocumentChunkRepository>();

builder.Services.AddSingleton<TextChunker>();

builder.Services.AddSingleton(
    new HttpClient
    {
        BaseAddress =
            new Uri(
                "http://localhost:11434")
    });

builder.Services.AddSingleton<IEmbeddingGenerator>(
    serviceProvider =>
        new OllamaEmbeddingGenerator(
            serviceProvider
                .GetRequiredService<HttpClient>(),
            model:
                "nomic-embed-text-v2-moe",
            inputPrefix:
                "search_document: ",
            dimensions:
                768));

builder.Services.AddSingleton<IAnswerGenerator>(
    serviceProvider =>
        new OllamaAnswerGenerator(
            serviceProvider
                .GetRequiredService<HttpClient>(),
            model:
                "qwen3:4b"));

builder.Services.AddScoped<AskDocumentsUseCase>();

builder.Services.AddScoped<
    IDocumentChunkEmbeddingRepository,
    EfDocumentChunkEmbeddingRepository>();

builder.Services.AddSingleton(
    serviceProvider =>
    {
        var configuration =
            serviceProvider.GetRequiredService<IConfiguration>();

        var connectionString =
            configuration["Messaging:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Service Bus connection string was not found.");

        return new ServiceBusClient(
            connectionString);
    });

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.RunAsync();