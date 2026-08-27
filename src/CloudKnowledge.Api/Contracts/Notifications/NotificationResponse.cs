namespace CloudKnowledge.Api.Contracts.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? Target,
    DateTime CreatedAtUtc,
    bool IsRead);
