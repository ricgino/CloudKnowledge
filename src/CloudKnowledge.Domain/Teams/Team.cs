namespace CloudKnowledge.Domain.Teams;

public sealed class Team
{
    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Team(
        Guid id,
        string name,
        DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        CreatedAtUtc = createdAtUtc;
    }

    public static Team Create(
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Team name cannot be empty.",
                nameof(name));
        }

        return new Team(
            Guid.NewGuid(),
            name.Trim(),
            DateTime.UtcNow);
    }
}