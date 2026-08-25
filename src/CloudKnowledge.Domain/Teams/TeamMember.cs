namespace CloudKnowledge.Domain.Teams;

public sealed class TeamMember
{
    public Guid TeamId { get; private set; }

    public Guid UserId { get; private set; }

    public TeamRole Role { get; private set; }

    public DateTime JoinedAtUtc { get; private set; }

    private TeamMember(
        Guid teamId,
        Guid userId,
        TeamRole role,
        DateTime joinedAtUtc)
    {
        TeamId = teamId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public static TeamMember Create(
        Guid teamId,
        Guid userId,
        TeamRole role = TeamRole.Member)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException(
                "Team id cannot be empty.",
                nameof(teamId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User id cannot be empty.",
                nameof(userId));
        }

        return new TeamMember(
            teamId,
            userId,
            role,
            DateTime.UtcNow);
    }
}