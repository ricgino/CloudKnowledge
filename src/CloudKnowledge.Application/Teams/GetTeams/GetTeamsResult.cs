using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Teams.GetTeams;

public sealed record GetTeamsResult(
    Guid Id,
    string Name,
    Guid? ParentTeamId,
    bool IsMember,
    TeamRole? Role,
    bool CanManage);
