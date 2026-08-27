using CloudKnowledge.Domain.Notifications;

namespace CloudKnowledge.Application.Notifications;

public sealed record NotificationResult(
    Guid Id,
    Guid UserId,
    NotificationType Type,
    string Title,
    string Message,
    string? Target,
    DateTime CreatedAtUtc,
    bool IsRead);
