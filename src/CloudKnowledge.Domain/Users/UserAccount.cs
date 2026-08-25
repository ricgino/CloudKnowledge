namespace CloudKnowledge.Domain.Users;

public sealed class UserAccount
{
    public Guid Id { get; private set; }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private UserAccount(
        Guid id,
        string email,
        string displayName,
        DateTime createdAtUtc)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        CreatedAtUtc = createdAtUtc;
    }

    public static UserAccount Create(
        string email,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(
                "Email cannot be empty.",
                nameof(email));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Display name cannot be empty.",
                nameof(displayName));
        }

        return new UserAccount(
            Guid.NewGuid(),
            email.Trim(),
            displayName.Trim(),
            DateTime.UtcNow);
    }
}