using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Notifications;

public sealed class MarkNotificationReadUseCase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ICurrentUser _currentUser;

    public MarkNotificationReadUseCase(
        INotificationRepository notificationRepository,
        ICurrentUser currentUser)
    {
        _notificationRepository = notificationRepository;
        _currentUser = currentUser;
    }

    public async Task<bool> ExecuteAsync(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var notification =
            await _notificationRepository.GetByIdAsync(
                userId,
                notificationId,
                cancellationToken);

        if (notification is null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.MarkAsRead();

            await _notificationRepository.UpdateAsync(
                notification,
                cancellationToken);
        }

        return true;
    }
}
