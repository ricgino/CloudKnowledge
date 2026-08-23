using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.ProcessDocument;
using CloudKnowledge.Application.Documents.FailDocument;
using CloudKnowledge.Application.Documents.ProcessDocument.Exceptions;

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
        DocumentProcessingMessage? message;

        try
        {
            message =
                JsonSerializer.Deserialize<DocumentProcessingMessage>(
                    args.Message.Body.ToString());
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Invalid Service Bus message {MessageId}. Moving it to DLQ.",
                args.Message.MessageId);

            await args.DeadLetterMessageAsync(
                args.Message,
                "InvalidMessage",
                "The message body is not valid JSON.",
                args.CancellationToken);

            return;
        }

        if (message is null ||
            message.DocumentId == Guid.Empty)
        {
            await args.DeadLetterMessageAsync(
                args.Message,
                "InvalidDocumentId",
                "The message does not contain a valid DocumentId.",
                args.CancellationToken);

            return;
        }

        _logger.LogInformation(
            "Received document {DocumentId}. Delivery count: {DeliveryCount}.",
            message.DocumentId,
            args.Message.DeliveryCount);

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var useCase =
                scope.ServiceProvider
                    .GetRequiredService<ProcessDocumentUseCase>();

            await useCase.ExecuteAsync(
                message.DocumentId,
                args.CancellationToken);

            await args.CompleteMessageAsync(
                args.Message);

            _logger.LogInformation(
                "Document {DocumentId} processed successfully.",
                message.DocumentId);
        }
        catch (OperationCanceledException)
            when (args.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PermanentDocumentProcessingException exception)
        {
            _logger.LogError(
                exception,
                "Permanent processing failure for document {DocumentId}.",
                message.DocumentId);

            await MarkDocumentAsFailedAsync(
                message.DocumentId,
                args.CancellationToken);

            await args.DeadLetterMessageAsync(
                args.Message,
                "PermanentProcessingFailure",
                exception.Message,
                args.CancellationToken);
        }
        catch (TransientDocumentProcessingException exception)
        {
            var handled =
                await HandleRetryableFailureAsync(
                    message.DocumentId,
                    args,
                    exception);

            if (handled)
            {
                return;
            }

            throw;
        }
        catch (Exception exception)
        {
            var handled =
                await HandleRetryableFailureAsync(
                    message.DocumentId,
                    args,
                    exception);

            if (handled)
            {
                return;
            }

            throw;
        }
    }

    private async Task<bool> HandleRetryableFailureAsync(
        Guid documentId,
        ProcessMessageEventArgs args,
        Exception exception)
    {
        var maxDeliveryCount =
            _configuration.GetValue<int>(
                "Messaging:MaxDeliveryCount");

        if (maxDeliveryCount <= 0)
        {
            throw new InvalidOperationException(
                "Messaging:MaxDeliveryCount must be greater than zero.");
        }

        if (args.Message.DeliveryCount >= maxDeliveryCount)
        {
            _logger.LogError(
                exception,
                "Document {DocumentId} failed after {DeliveryCount} deliveries. " +
                "Moving message to DLQ.",
                documentId,
                args.Message.DeliveryCount);

            await MarkDocumentAsFailedAsync(
                documentId,
                args.CancellationToken);

            await args.DeadLetterMessageAsync(
                args.Message,
                "ProcessingRetriesExhausted",
                exception.Message,
                args.CancellationToken);

            return true;
        }

        _logger.LogWarning(
            exception,
            "Transient processing failure for document {DocumentId}. " +
            "Delivery {DeliveryCount}. Message will be retried.",
            documentId,
            args.Message.DeliveryCount);

        return false;
    }
    private async Task MarkDocumentAsFailedAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await using var scope =
            _scopeFactory.CreateAsyncScope();

        var failDocumentUseCase =
            scope.ServiceProvider
                .GetRequiredService<FailDocumentUseCase>();

        await failDocumentUseCase.ExecuteAsync(
            documentId,
            cancellationToken);
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