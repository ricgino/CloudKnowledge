using Azure.Messaging.ServiceBus;
using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.ProcessDocument;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Worker;
using Microsoft.EntityFrameworkCore;
using CloudKnowledge.Application.Documents.FailDocument;

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

        options.UseNpgsql(connectionString);
    });

builder.Services.AddScoped<
    IDocumentRepository,
    EfDocumentRepository>();

builder.Services.AddScoped<ProcessDocumentUseCase>();
builder.Services.AddScoped<FailDocumentUseCase>();

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