using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Teams.GetTeams;

public sealed class GetTeamsUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly ITeamRepository _teamRepository;

    public GetTeamsUseCase(
        ICurrentUser currentUser,
        ITeamRepository teamRepository)
    {
        _currentUser = currentUser;
        _teamRepository = teamRepository;
    }

    public async Task<IReadOnlyList<GetTeamsResult>> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        return await _teamRepository.GetForUserAsync(
            userId,
            cancellationToken);
    }
}
