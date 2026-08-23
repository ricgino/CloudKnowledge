using Azure.Messaging.ServiceBus;
using CloudKnowledge.Worker;

var builder =
    Host.CreateApplicationBuilder(args);

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