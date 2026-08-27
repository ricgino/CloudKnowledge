namespace CloudKnowledge.Application.Notifications.DocumentReady;

public sealed record DocumentReadyNotificationAudience(
    string FileName,
    Guid OwnerUserId,
    string OwnerDisplayName,
    IReadOnlyList<Guid> RecipientUserIds);
