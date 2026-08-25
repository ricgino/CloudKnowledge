using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Teams.CreateTeam;

public sealed class CreateTeamUseCase
{
    private readonly ITeamRepository
        _teamRepository;

    private readonly ICurrentUser
        _currentUser;

    public CreateTeamUseCase(
        ITeamRepository teamRepository,
        ICurrentUser currentUser)
    {
        _teamRepository =
            teamRepository;

        _currentUser =
            currentUser;
    }

    public async Task<CreateTeamResult> ExecuteAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var team =
            Team.Create(
                name);

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
            team.Id,
            team.Name,
            ownerMembership.Role);
    }
}