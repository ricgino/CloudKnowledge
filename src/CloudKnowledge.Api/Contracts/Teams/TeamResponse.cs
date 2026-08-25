namespace CloudKnowledge.Api.Contracts.Teams;

public sealed record TeamResponse(
    Guid Id,
    string Name,
    string Role);