using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CloudKnowledge.Application.Notifications.DocumentReady;

namespace CloudKnowledge.Api.Notifications;

public sealed class DocumentReadyNotificationsWorker
    : BackgroundService
{
    private readonly ILogger<DocumentReadyNotificationsWorker> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationStreamBroker _streamBroker;
    private ServiceBusProcessor? _processor;

    public DocumentReadyNotificationsWorker(
        ILogger<DocumentReadyNotificationsWorker> logger,
        ServiceBusClient serviceBusClient,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        NotificationStreamBroker streamBroker)
    {
        _logger = logger;
        _serviceBusClient = serviceBusClient;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _streamBroker = streamBroker;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var queueName =
            _configuration["Messaging:NotificationsQueueName"]
            ?? throw new InvalidOperationException(
                "Notifications queue name was not found.");

        _processor =
            await StartProcessorWithRetryAsync(
                queueName,
                stoppingToken);

        _logger.LogInformation(
            "Notification worker started. Listening on queue {QueueName}.",
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
            if (_processor is not null)
            {
                await _processor.StopProcessingAsync(
                    CancellationToken.None);

                await _processor.DisposeAsync();
            }
        }
    }

    private async Task<ServiceBusProcessor> StartProcessorWithRetryAsync(
        string queueName,
        CancellationToken stoppingToken)
    {
        var retrySeconds =
            _configuration.GetValue<int>(
                "Messaging:StartupRetrySeconds");

        if (retrySeconds <= 0)
        {
            retrySeconds = 5;
        }

        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var processor =
                _serviceBusClient.CreateProcessor(
                    queueName,
                    new ServiceBusProcessorOptions
                    {
                        AutoCompleteMessages = false,
                        MaxConcurrentCalls = 1
                    });

            processor.ProcessMessageAsync +=
                ProcessMessageAsync;

            processor.ProcessErrorAsync +=
                ProcessErrorAsync;

            try
            {
                await processor.StartProcessingAsync(
                    stoppingToken);

                return processor;
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                await processor.DisposeAsync();
                throw;
            }
            catch (Exception exception)
            {
                await processor.DisposeAsync();

                _logger.LogWarning(
                    exception,
                    "Notification queue is not ready. Retrying in {RetrySeconds} seconds.",
                    retrySeconds);

                await Task.Delay(
                    TimeSpan.FromSeconds(
                        retrySeconds),
                    stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(
        ProcessMessageEventArgs args)
    {
        DocumentReadyMessage? message;

        try
        {
            message =
                JsonSerializer.Deserialize<DocumentReadyMessage>(
                    args.Message.Body.ToString());
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Invalid document-ready message {MessageId}.",
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

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var useCase =
                scope.ServiceProvider.GetRequiredService<
                    CreateDocumentReadyNotificationsUseCase>();

            var notifications =
                await useCase.ExecuteAsync(
                    message.DocumentId,
                    args.CancellationToken);

            foreach (var notification in
                     notifications)
            {
                _streamBroker.Publish(
                    notification.UserId,
                    notification);
            }

            await args.CompleteMessageAsync(
                args.Message,
                args.CancellationToken);

            _logger.LogInformation(
                "Created {NotificationCount} notifications for ready document {DocumentId}.",
                notifications.Count,
                message.DocumentId);
        }
        catch (OperationCanceledException)
            when (args.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to create notifications for ready document {DocumentId}.",
                message.DocumentId);

            await args.AbandonMessageAsync(
                args.Message,
                cancellationToken:
                    args.CancellationToken);
        }
    }

    private Task ProcessErrorAsync(
        ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Notification Service Bus error. Entity: {EntityPath}; Source: {ErrorSource}.",
            args.EntityPath,
            args.ErrorSource);

        return Task.CompletedTask;
    }
}
