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

builder.Services.AddScoped<
    IPdfNativeTextExtractor,
    PdfPigDocumentTextExtractor>();

builder.Services.AddSingleton<
    IExternalCommandRunner,
    SystemExternalCommandRunner>();

builder.Services.AddScoped<IPdfOcrTextExtractor>(
    serviceProvider =>
    {
        var configuration =
            serviceProvider.GetRequiredService<IConfiguration>();

        var languages =
            configuration["Ocr:Languages"]
            ?? "eng+ita";

        var dpi =
            configuration.GetValue<int>(
                "Ocr:Dpi");

        if (dpi <= 0)
        {
            dpi = 300;
        }

        return new TesseractPdfOcrTextExtractor(
            serviceProvider
                .GetRequiredService<IExternalCommandRunner>(),
            languages,
            dpi);
    });

builder.Services.AddScoped<PdfDocumentTextExtractor>();
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

var aiConfiguration =
    AiProviderConfiguration.From(
        builder.Configuration,
        requireAnswerGenerator: false);

builder.Services.AddSingleton(
    aiConfiguration);

builder.Services.AddSingleton(
    _ =>
        new HttpClient
        {
            BaseAddress =
                aiConfiguration.BaseUrl
        });

builder.Services.AddSingleton<IEmbeddingGenerator>(
    serviceProvider =>
    {
        var httpClient =
            serviceProvider
                .GetRequiredService<HttpClient>();

        if (aiConfiguration.IsAzureOpenAi)
        {
            return new AzureOpenAiEmbeddingGenerator(
                httpClient,
                aiConfiguration.EmbeddingModel,
                aiConfiguration.ApiKey!,
                aiConfiguration.EmbeddingDimensions);
        }

        return new OllamaEmbeddingGenerator(
            httpClient,
            aiConfiguration.EmbeddingModel,
            inputPrefix:
                "search_document: ",
            aiConfiguration.EmbeddingDimensions);
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
