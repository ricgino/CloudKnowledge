using CloudKnowledge.Domain.Notifications;

namespace CloudKnowledge.Application.Notifications;

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> GetRecentAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken);

    Task<Notification?> GetByIdAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken);

    Task<bool> AddIfMissingAsync(
        Notification notification,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        Notification notification,
        CancellationToken cancellationToken);
}
