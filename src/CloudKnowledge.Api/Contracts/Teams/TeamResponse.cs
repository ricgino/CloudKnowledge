namespace CloudKnowledge.Api.Contracts.Teams;

public sealed record TeamResponse(
    Guid Id,
    string Name,
    Guid? ParentTeamId,
    bool IsMember,
    string? Role,
    bool CanManage);
