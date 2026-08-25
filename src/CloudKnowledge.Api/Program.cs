using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Application.Documents.GetDocument;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CloudKnowledge.Application.Documents.GetDocuments;
using Azure.Storage.Blobs;
using Azure.Messaging.ServiceBus;
using Pgvector.EntityFrameworkCore;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Api.Authentication;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Infrastructure.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Teams.CreateTeam;
using CloudKnowledge.Infrastructure.Teams;
using CloudKnowledge.Application.Teams.AddTeamMember;
using CloudKnowledge.Application.Documents.Sharing;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(
        builder.Configuration.GetSection(
            "AzureAd"));

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<
    IUserAccountRepository,
    EfUserAccountRepository>();

builder.Services.AddScoped<
    ICurrentUser,
    HttpCurrentUser>();

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

        options.UseNpgsql(
            connectionString,
            npgsqlOptions =>
                npgsqlOptions.UseVector());
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
                "search_query: ",
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
    IDocumentSemanticSearchRepository,
    EfDocumentSemanticSearchRepository>();

builder.Services.AddScoped<
    SearchDocumentsUseCase>();

builder.Services.AddScoped<
    IDocumentAccessRepository,
    EfDocumentAccessRepository>();

builder.Services.AddScoped<
    ITeamRepository,
    EfTeamRepository>();

builder.Services.AddScoped<
    CreateTeamUseCase>();    

builder.Services.AddScoped<
    IUserDirectoryRepository,
    EfUserDirectoryRepository>();

builder.Services.AddScoped<
    ITeamMembershipRepository,
    EfTeamMembershipRepository>();

builder.Services.AddScoped<
    AddTeamMemberUseCase>();

builder.Services.AddScoped<
    IDocumentSharingRepository,
    EfDocumentSharingRepository>();

builder.Services.AddScoped<
    ShareDocumentWithTeamUseCase>();

builder.Services.AddScoped<
    UnshareDocumentFromTeamUseCase>();
    
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}