using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Teams.CreateTeam;

public sealed record CreateTeamResult(
    Guid Id,
    string Name,
    TeamRole Role);