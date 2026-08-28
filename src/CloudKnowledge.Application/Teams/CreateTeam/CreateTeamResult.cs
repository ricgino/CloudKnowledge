using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Teams.CreateTeam;

public sealed record CreateTeamResult(
    CreateTeamStatus Status,
    Guid? Id,
    string? Name,
    Guid? ParentTeamId,
    TeamRole? Role);
