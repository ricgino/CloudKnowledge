namespace CloudKnowledge.Api.Contracts.Teams;

public sealed record TeamMemberResponse(
    Guid UserId,
    string Email,
    string Role);