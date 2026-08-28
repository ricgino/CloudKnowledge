namespace CloudKnowledge.Api.Contracts.Documents;

public sealed record DocumentAccessTeamResponse(
    Guid Id,
    string Name,
    string Path);
