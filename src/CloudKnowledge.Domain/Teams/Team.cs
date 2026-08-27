namespace CloudKnowledge.Domain.Teams;

public sealed class Team
{
    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public Guid? ParentTeamId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Team(
        Guid id,
        string name,
        Guid? parentTeamId,
        DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        ParentTeamId = parentTeamId;
        CreatedAtUtc = createdAtUtc;
    }

    public static Team Create(
        string name,
        Guid? parentTeamId = null)
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
            parentTeamId,
            DateTime.UtcNow);
    }
}
