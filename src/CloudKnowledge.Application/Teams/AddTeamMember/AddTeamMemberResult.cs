using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Teams.AddTeamMember;

public sealed record AddTeamMemberResult(
    AddTeamMemberStatus Status,
    Guid? UserId = null,
    string? Email = null,
    TeamRole? Role = null);