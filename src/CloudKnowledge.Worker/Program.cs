using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.FailDocument;
using CloudKnowledge.Application.Documents.ProcessDocument;
using CloudKnowledge.Application.Notifications.DocumentReady;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Notifications;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Worker;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddScoped<PdfPigDocumentTextExtractor>();
builder.Services.AddScoped<OpenXmlDocumentTextExtractor>();
builder.Services.AddScoped<PlainTextDocumentTextExtractor>();
builder.Services.AddScoped<
    IDocumentTextExtractor,
    DocumentTextExtractorDispatcher>();

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
    serviceProvider =>
    {
        var configuration =
            serviceProvider.GetRequiredService<IConfiguration>();

        var baseUrl =
            configuration["Ai:BaseUrl"]
            ?? throw new InvalidOperationException(
                "AI base URL was not found.");

        return new HttpClient
        {
            BaseAddress =
                new Uri(baseUrl)
        };
    });

builder.Services.AddSingleton<IEmbeddingGenerator>(
    serviceProvider =>
    {
        var configuration =
            serviceProvider.GetRequiredService<IConfiguration>();

        var model =
            configuration["Ai:EmbeddingModel"]
            ?? throw new InvalidOperationException(
                "AI embedding model was not found.");

        var dimensions =
            configuration.GetValue<int>(
                "Ai:EmbeddingDimensions");

        return new OllamaEmbeddingGenerator(
            serviceProvider
                .GetRequiredService<HttpClient>(),
            model,
            inputPrefix:
                "search_document: ",
            dimensions);
    });

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

builder.Services.AddSingleton<IDocumentReadyPublisher>(
    serviceProvider =>
    {
        var configuration =
            serviceProvider.GetRequiredService<IConfiguration>();

        var queueName =
            configuration["Messaging:NotificationsQueueName"]
            ?? throw new InvalidOperationException(
                "Notifications queue name was not found.");

        var client =
            serviceProvider.GetRequiredService<ServiceBusClient>();

        return new AzureServiceBusDocumentReadyPublisher(
            client.CreateSender(
                queueName));
    });

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.RunAsync();
