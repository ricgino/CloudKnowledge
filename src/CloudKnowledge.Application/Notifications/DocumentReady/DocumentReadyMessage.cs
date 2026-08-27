namespace CloudKnowledge.Application.Notifications.DocumentReady;

public sealed record DocumentReadyMessage(
    Guid DocumentId);
