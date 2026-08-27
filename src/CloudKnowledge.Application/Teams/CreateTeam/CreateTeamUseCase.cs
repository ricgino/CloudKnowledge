using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Teams.CreateTeam;

public sealed class CreateTeamUseCase
{
    private readonly ITeamRepository
        _teamRepository;

    private readonly ITeamMembershipRepository
        _teamMembershipRepository;

    private readonly ICurrentUser
        _currentUser;

    public CreateTeamUseCase(
        ITeamRepository teamRepository,
        ITeamMembershipRepository teamMembershipRepository,
        ICurrentUser currentUser)
    {
        _teamRepository =
            teamRepository;

        _teamMembershipRepository =
            teamMembershipRepository;

        _currentUser =
            currentUser;
    }

    public async Task<CreateTeamResult> ExecuteAsync(
        string name,
        Guid? parentTeamId,
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        if (parentTeamId.HasValue)
        {
            var parent =
                await _teamRepository.GetByIdAsync(
                    parentTeamId.Value,
                    cancellationToken);

            if (parent is null)
            {
                return new CreateTeamResult(
                    CreateTeamStatus.ParentNotFoundOrNotMember,
                    null,
                    null,
                    parentTeamId,
                    null);
            }

            var membership =
                await _teamMembershipRepository.GetMembershipAsync(
                    parentTeamId.Value,
                    userId,
                    cancellationToken);

            if (membership is null)
            {
                return new CreateTeamResult(
                    CreateTeamStatus.ParentNotFoundOrNotMember,
                    null,
                    null,
                    parentTeamId,
                    null);
            }

            if (membership.Role is not TeamRole.Admin and
                not TeamRole.Owner)
            {
                return new CreateTeamResult(
                    CreateTeamStatus.Forbidden,
                    null,
                    null,
                    parentTeamId,
                    null);
            }
        }

        var team =
            Team.Create(
                name,
                parentTeamId);

        var ownerMembership =
            TeamMember.Create(
                team.Id,
                userId,
                TeamRole.Owner);

        await _teamRepository.AddAsync(
            team,
            ownerMembership,
            cancellationToken);

        return new CreateTeamResult(
            CreateTeamStatus.Created,
            team.Id,
            team.Name,
            team.ParentTeamId,
            ownerMembership.Role);
    }
}
