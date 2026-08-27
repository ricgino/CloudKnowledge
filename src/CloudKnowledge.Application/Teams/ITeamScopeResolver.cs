namespace CloudKnowledge.Application.Teams;

public interface ITeamScopeResolver
{
    Task<Guid[]> ResolveAllowedTeamIdsAsync(
        Guid userId,
        Guid selectedTeamId,
        bool includeDescendants,
        CancellationToken cancellationToken);
}
