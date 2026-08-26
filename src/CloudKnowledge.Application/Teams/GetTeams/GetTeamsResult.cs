using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Teams.GetTeams;

public sealed record GetTeamsResult(
    Guid Id,
    string Name,
    TeamRole Role);
