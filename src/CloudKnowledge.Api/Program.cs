using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Application.Documents.GetDocument;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CloudKnowledge.Application.Documents.GetDocuments;
using Azure.Storage.Blobs;
using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();

builder.Services.AddDbContext<CloudKnowledgeDbContext>(
    (serviceProvider, options) =>
    {
        var configuration =
            serviceProvider.GetRequiredService<IConfiguration>();

        var connectionString =
            configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' was not found.");

        options.UseNpgsql(connectionString);
    });

builder.Services.AddScoped<
    IDocumentRepository,
    EfDocumentRepository>();

builder.Services.AddScoped<CreateDocumentUseCase>();
builder.Services.AddScoped<GetDocumentUseCase>();
builder.Services.AddScoped<GetDocumentsUseCase>();

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

builder.Services.AddSingleton(
    serviceProvider =>
    {
        var configuration =
            serviceProvider.GetRequiredService<IConfiguration>();

        var queueName =
            configuration["Messaging:QueueName"]
            ?? throw new InvalidOperationException(
                "Service Bus queue name was not found.");

        var client =
            serviceProvider.GetRequiredService<ServiceBusClient>();

        return client.CreateSender(
            queueName);
    });

builder.Services.AddScoped<
    IDocumentProcessingQueue,
    AzureServiceBusDocumentProcessingQueue>();    

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}