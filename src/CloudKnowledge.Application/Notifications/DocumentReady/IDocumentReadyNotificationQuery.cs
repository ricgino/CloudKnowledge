namespace CloudKnowledge.Application.Notifications.DocumentReady;

public interface IDocumentReadyNotificationQuery
{
    Task<DocumentReadyNotificationAudience?> GetAudienceAsync(
        Guid documentId,
        CancellationToken cancellationToken);
}
