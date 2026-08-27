namespace CloudKnowledge.Api.Contracts.Documents;

public sealed record DocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    string Status,
    bool IsOwner = false,
    IReadOnlyList<DocumentAccessTeamResponse>? SharedTeams = null);
