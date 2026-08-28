using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using CloudKnowledge.Api.Authentication;
using CloudKnowledge.Api.Database;
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
using CloudKnowledge.Application.Teams.DeleteTeam;
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

var migrationOnly =
    DatabaseStartupMode.IsMigrationOnly(args);

var builder =
    WebApplication.CreateBuilder(
        DatabaseStartupMode.RemoveMigrationArgument(args));

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

var aiConfiguration =
    AiProviderConfiguration.From(
        builder.Configuration,
        requireAnswerGenerator: true);

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
                aiConfiguration.ApiVersion!,
                aiConfiguration.EmbeddingDimensions);
        }

        return new OllamaEmbeddingGenerator(
            httpClient,
            aiConfiguration.EmbeddingModel,
            inputPrefix:
                "search_query: ",
            aiConfiguration.EmbeddingDimensions);
    });

builder.Services.AddSingleton<IAnswerGenerator>(
    serviceProvider =>
    {
        var httpClient =
            serviceProvider
                .GetRequiredService<HttpClient>();

        if (aiConfiguration.IsAzureOpenAi)
        {
            return new AzureOpenAiAnswerGenerator(
                httpClient,
                aiConfiguration.AnswerModel!,
                aiConfiguration.ApiKey!,
                aiConfiguration.ApiVersion!,
                aiConfiguration.AnswerTemperature,
                aiConfiguration.AnswerMaxTokens);
        }

        return new OllamaAnswerGenerator(
            httpClient,
            aiConfiguration.AnswerModel!,
            aiConfiguration.AnswerTemperature,
            aiConfiguration.AnswerMaxTokens,
            serviceProvider
                .GetRequiredService<ILogger<OllamaAnswerGenerator>>());
    });

builder.Services.AddScoped<AskDocumentsUseCase>();

builder.Services.AddScoped<
    ITeamScopeResolver,
    EfTeamScopeResolver>();

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
    ITeamDeletionRepository,
    EfTeamDeletionRepository>();

builder.Services.AddScoped<
    DeleteTeamUseCase>();

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

if (migrationOnly ||
    app.Configuration.GetValue<bool>(
        "Database:ApplyMigrationsOnStartup"))
{
    await using var scope =
        app.Services.CreateAsyncScope();

    var dbContext =
        scope.ServiceProvider
            .GetRequiredService<CloudKnowledgeDbContext>();

    await dbContext.Database.MigrateAsync();

    if (migrationOnly)
    {
        return;
    }
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
