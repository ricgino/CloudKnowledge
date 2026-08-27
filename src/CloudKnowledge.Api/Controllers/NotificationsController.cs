using System.Text.Json;
using CloudKnowledge.Api.Contracts.Notifications;
using CloudKnowledge.Api.Notifications;
using CloudKnowledge.Application.Notifications;
using CloudKnowledge.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace CloudKnowledge.Api.Controllers;

[Authorize]
[RequiredScope("access_as_user")]
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController
    : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly GetNotificationsUseCase _getNotificationsUseCase;
    private readonly MarkNotificationReadUseCase _markNotificationReadUseCase;
    private readonly ICurrentUser _currentUser;
    private readonly NotificationStreamBroker _streamBroker;

    public NotificationsController(
        GetNotificationsUseCase getNotificationsUseCase,
        MarkNotificationReadUseCase markNotificationReadUseCase,
        ICurrentUser currentUser,
        NotificationStreamBroker streamBroker)
    {
        _getNotificationsUseCase = getNotificationsUseCase;
        _markNotificationReadUseCase = markNotificationReadUseCase;
        _currentUser = currentUser;
        _streamBroker = streamBroker;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> GetAll(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var notifications =
            await _getNotificationsUseCase.ExecuteAsync(
                take,
                cancellationToken);

        return Ok(
            notifications
                .Select(Map)
                .ToList());
    }

    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var found =
            await _markNotificationReadUseCase.ExecuteAsync(
                notificationId,
                cancellationToken);

        return found
            ? NoContent()
            : NotFound();
    }

    [HttpGet("stream")]
    public async Task Stream(
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await Response.WriteAsync(
            ": connected\n\n",
            cancellationToken);

        await Response.Body.FlushAsync(
            cancellationToken);

        await using var subscription =
            _streamBroker.Subscribe(
                userId);

        while (!cancellationToken.IsCancellationRequested)
        {
            var waitForNotification =
                subscription.Reader
                    .WaitToReadAsync(
                        cancellationToken)
                    .AsTask();

            var heartbeat =
                Task.Delay(
                    TimeSpan.FromSeconds(15),
                    cancellationToken);

            var completed =
                await Task.WhenAny(
                    waitForNotification,
                    heartbeat);

            if (completed == heartbeat)
            {
                await Response.WriteAsync(
                    ": keep-alive\n\n",
                    cancellationToken);

                await Response.Body.FlushAsync(
                    cancellationToken);

                continue;
            }

            if (!await waitForNotification)
            {
                break;
            }

            while (subscription.Reader.TryRead(
                       out var notification))
            {
                var response =
                    Map(notification);

                var json =
                    JsonSerializer.Serialize(
                        response,
                        JsonOptions);

                await Response.WriteAsync(
                    $"event: notification\ndata: {json}\n\n",
                    cancellationToken);

                await Response.Body.FlushAsync(
                    cancellationToken);
            }
        }
    }

    private static NotificationResponse Map(
        NotificationResult notification)
    {
        return new NotificationResponse(
            notification.Id,
            notification.Type.ToString(),
            notification.Title,
            notification.Message,
            notification.Target,
            notification.CreatedAtUtc,
            notification.IsRead);
    }
}
