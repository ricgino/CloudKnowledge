using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Teams;

public interface ITeamRepository
{
    Task AddAsync(
        Team team,
        TeamMember ownerMembership,
        CancellationToken cancellationToken);
}