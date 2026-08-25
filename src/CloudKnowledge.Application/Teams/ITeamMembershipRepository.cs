using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Teams;

public interface ITeamMembershipRepository
{
    Task<TeamMember?> GetMembershipAsync(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken);

    Task AddAsync(
        TeamMember membership,
        CancellationToken cancellationToken);
}