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

builder.Services.AddSingleton<IEmbeddingGenerator>(
    new DevelopmentHashEmbeddingGenerator(
        dimensions: 1536));

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