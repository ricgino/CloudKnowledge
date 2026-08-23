using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.ProcessDocument;

namespace CloudKnowledge.Worker;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IConfiguration _configuration;
    private ServiceBusProcessor? _processor;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(
        ILogger<Worker> logger,
        ServiceBusClient serviceBusClient,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _serviceBusClient = serviceBusClient;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var queueName =
            _configuration["Messaging:QueueName"]
            ?? throw new InvalidOperationException(
                "Service Bus queue name was not found.");

        _processor =
            _serviceBusClient.CreateProcessor(
                queueName,
                new ServiceBusProcessorOptions
                {
                    AutoCompleteMessages = false,
                    MaxConcurrentCalls = 1
                });

        _processor.ProcessMessageAsync +=
            ProcessMessageAsync;

        _processor.ProcessErrorAsync +=
            ProcessErrorAsync;

        await _processor.StartProcessingAsync(
            stoppingToken);

        _logger.LogInformation(
            "Document worker started. Listening on queue {QueueName}.",
            queueName);

        try
        {
            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await _processor.StopProcessingAsync(
                CancellationToken.None);

            await _processor.DisposeAsync();
        }
    }

    private async Task ProcessMessageAsync(
    ProcessMessageEventArgs args)
    {
        var json =
            args.Message.Body.ToString();

        var message =
            JsonSerializer.Deserialize<DocumentProcessingMessage>(
                json);

        if (message is null)
        {
            throw new InvalidOperationException(
                "Document processing message could not be deserialized.");
        }

        _logger.LogInformation(
            "Received document {DocumentId} for processing.",
            message.DocumentId);

        await using var scope =
            _scopeFactory.CreateAsyncScope();

        var useCase =
            scope.ServiceProvider
                .GetRequiredService<ProcessDocumentUseCase>();

        await useCase.ExecuteAsync(
            message.DocumentId,
            args.CancellationToken);

        _logger.LogInformation(
            "Document {DocumentId} processed successfully.",
            message.DocumentId);

        await args.CompleteMessageAsync(
            args.Message);
    }

    private Task ProcessErrorAsync(
        ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus error. Entity: {EntityPath}.",
            args.EntityPath);

        return Task.CompletedTask;
    }
}