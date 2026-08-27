using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using CloudKnowledge.Api.Authentication;
using CloudKnowledge.Api.Notifications;
using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Application.Documents.DeleteDocument;
using CloudKnowledge.Application.Documents.DownloadDocument;
using CloudKnowledge.Application.Documents.GetDocument;
using CloudKnowledge.Application.Documents.GetDocuments;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Documents.Sharing;
using CloudKnowledge.Application.Notifications;
using CloudKnowledge.Application.Notifications.DocumentReady;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Teams.AddTeamMember;
using CloudKnowledge.Application.Teams.CreateTeam;
using CloudKnowledge.Application.Teams.GetTeams;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Notifications;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Infrastructure.Teams;
using CloudKnowledge.Infrastructure.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(
        builder.Configuration.GetSection(
            "AzureAd"));

builder.Services.AddAuthorization();

builder.Services.AddCors(
    options =>
    {
        options.AddPolicy(
            "CloudKnowledgeWeb",
            policy =>
            {
                var allowedOrigins =
                    builder.Configuration
                        .GetSection("Cors:AllowedOrigins")
                        .Get<string[]>()
                    ?? [];

                if (allowedOrigins.Length == 0)
                {
                    throw new InvalidOperationException(
                        "At least one CORS allowed origin must be configured.");
                }

                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

builder.Services.AddScoped<
    IUserAccountRepository,
    EfUserAccountRepository>();

builder.Services.AddScoped<
    ICurrentUser,
    HttpCurrentUser>();

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

builder.Services.AddScoped<
    IDocumentDeletionRepository,
    EfDocumentDeletionRepository>();

builder.Services.AddScoped<CreateDocumentUseCase>();
builder.Services.AddScoped<GetDocumentUseCase>();
builder.Services.AddScoped<GetDocumentsUseCase>();
builder.Services.AddScoped<DeleteDocumentUseCase>();
builder.Services.AddScoped<DownloadDocumentUseCase>();

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
    IDocumentDeletionStorage,
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

builder.Services.AddOpenApi();

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
                "search_query: ",
            dimensions);
    });

builder.Services.AddSingleton<IAnswerGenerator>(
    serviceProvider =>
    {
        var configuration =
            serviceProvider.GetRequiredService<IConfiguration>();

        var model =
            configuration["Ai:AnswerModel"]
            ?? throw new InvalidOperationException(
                "AI answer model was not found.");

        return new OllamaAnswerGenerator(
            serviceProvider
                .GetRequiredService<HttpClient>(),
            model);
    });

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
    GetTeamsUseCase>();

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

builder.Services.AddScoped<
    INotificationRepository,
    EfNotificationRepository>();

builder.Services.AddScoped<
    IDocumentReadyNotificationQuery,
    EfDocumentReadyNotificationQuery>();

builder.Services.AddScoped<GetNotificationsUseCase>();
builder.Services.AddScoped<MarkNotificationReadUseCase>();
builder.Services.AddScoped<CreateDocumentReadyNotificationsUseCase>();

builder.Services.AddSingleton<NotificationStreamBroker>();

if (builder.Configuration.GetValue<bool>(
        "Messaging:NotificationsEnabled"))
{
    builder.Services.AddHostedService<
        DocumentReadyNotificationsWorker>();
}

var app = builder.Build();

if (app.Configuration.GetValue<bool>(
        "Database:ApplyMigrationsOnStartup"))
{
    await using var scope =
        app.Services.CreateAsyncScope();

    var dbContext =
        scope.ServiceProvider
            .GetRequiredService<CloudKnowledgeDbContext>();

    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.MapOpenApi();
}

app.UseCors(
    "CloudKnowledgeWeb");

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks(
    "/health");

app.MapControllers();

app.Run();

public partial class Program
{
}
