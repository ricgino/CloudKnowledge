namespace CloudKnowledge.Api.Contracts.Teams;

public sealed record CreateTeamRequest(
    string Name,
    Guid? ParentTeamId = null);
