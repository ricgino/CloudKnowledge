using CloudKnowledge.Domain.Notifications;

namespace CloudKnowledge.Application.Notifications.DocumentReady;

public sealed class CreateDocumentReadyNotificationsUseCase
{
    private readonly IDocumentReadyNotificationQuery _audienceQuery;
    private readonly INotificationRepository _notificationRepository;

    public CreateDocumentReadyNotificationsUseCase(
        IDocumentReadyNotificationQuery audienceQuery,
        INotificationRepository notificationRepository)
    {
        _audienceQuery = audienceQuery;
        _notificationRepository = notificationRepository;
    }

    public async Task<IReadOnlyList<NotificationResult>> ExecuteAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var audience =
            await _audienceQuery.GetAudienceAsync(
                documentId,
                cancellationToken);

        if (audience is null ||
            audience.RecipientUserIds.Count == 0)
        {
            return Array.Empty<NotificationResult>();
        }

        var created =
            new List<NotificationResult>();

        var ownerName =
            string.IsNullOrWhiteSpace(
                audience.OwnerDisplayName)
                ? "A team member"
                : audience.OwnerDisplayName;

        foreach (var recipientUserId in
                 audience.RecipientUserIds.Distinct())
        {
            if (recipientUserId ==
                audience.OwnerUserId)
            {
                continue;
            }

            var notification =
                Notification.Create(
                    recipientUserId,
                    NotificationType.DocumentReady,
                    "New team document is ready",
                    $"{ownerName} shared {audience.FileName}. It is now ready to search and ask.",
                    $"document-ready:{documentId:D}",
                    target: "documents");

            var wasAdded =
                await _notificationRepository.AddIfMissingAsync(
                    notification,
                    cancellationToken);

            if (wasAdded)
            {
                created.Add(
                    GetNotificationsUseCase.Map(
                        notification));
            }
        }

        return created;
    }
}
