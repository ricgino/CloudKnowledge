using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Notifications;

namespace CloudKnowledge.Application.Notifications;

public sealed class GetNotificationsUseCase
{
    private const int MaxTake = 100;

    private readonly INotificationRepository _notificationRepository;
    private readonly ICurrentUser _currentUser;

    public GetNotificationsUseCase(
        INotificationRepository notificationRepository,
        ICurrentUser currentUser)
    {
        _notificationRepository = notificationRepository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<NotificationResult>> ExecuteAsync(
        int take,
        CancellationToken cancellationToken)
    {
        if (take < 1 || take > MaxTake)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                $"Take must be between 1 and {MaxTake}.");
        }

        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var notifications =
            await _notificationRepository.GetRecentAsync(
                userId,
                take,
                cancellationToken);

        return notifications
            .Select(Map)
            .ToList();
    }

    internal static NotificationResult Map(
        Notification notification)
    {
        return new NotificationResult(
            notification.Id,
            notification.UserId,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.Target,
            notification.CreatedAtUtc,
            notification.IsRead);
    }
}
