namespace CloudKnowledge.Domain.Notifications;

public sealed class Notification
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; }

    public string Message { get; private set; }

    public string? Target { get; private set; }

    public string DeduplicationKey { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    public bool IsRead =>
        ReadAtUtc is not null;

    private Notification(
        Guid id,
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string? target,
        string deduplicationKey,
        DateTime createdAtUtc,
        DateTime? readAtUtc)
    {
        Id = id;
        UserId = userId;
        Type = type;
        Title = title;
        Message = message;
        Target = target;
        DeduplicationKey = deduplicationKey;
        CreatedAtUtc = createdAtUtc;
        ReadAtUtc = readAtUtc;
    }

    public static Notification Create(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string deduplicationKey,
        string? target = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User id cannot be empty.",
                nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "Notification title cannot be empty.",
                nameof(title));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "Notification message cannot be empty.",
                nameof(message));
        }

        if (string.IsNullOrWhiteSpace(deduplicationKey))
        {
            throw new ArgumentException(
                "Deduplication key cannot be empty.",
                nameof(deduplicationKey));
        }

        return new Notification(
            Guid.NewGuid(),
            userId,
            type,
            title.Trim(),
            message.Trim(),
            string.IsNullOrWhiteSpace(target)
                ? null
                : target.Trim(),
            deduplicationKey.Trim(),
            DateTime.UtcNow,
            null);
    }

    public void MarkAsRead()
    {
        if (ReadAtUtc is not null)
        {
            return;
        }

        ReadAtUtc = DateTime.UtcNow;
    }
}
