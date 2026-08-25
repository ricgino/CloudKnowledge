using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Teams.AddTeamMember;

public sealed class AddTeamMemberUseCase
{
    private readonly ITeamMembershipRepository
        _teamMembershipRepository;

    private readonly IUserDirectoryRepository
        _userDirectoryRepository;

    private readonly ICurrentUser
        _currentUser;

    public AddTeamMemberUseCase(
        ITeamMembershipRepository teamMembershipRepository,
        IUserDirectoryRepository userDirectoryRepository,
        ICurrentUser currentUser)
    {
        _teamMembershipRepository =
            teamMembershipRepository;

        _userDirectoryRepository =
            userDirectoryRepository;

        _currentUser =
            currentUser;
    }

    public async Task<AddTeamMemberResult> ExecuteAsync(
        Guid teamId,
        string email,
        CancellationToken cancellationToken)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException(
                "Team id cannot be empty.",
                nameof(teamId));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(
                "Email cannot be empty.",
                nameof(email));
        }

        var currentUserId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var currentMembership =
            await _teamMembershipRepository
                .GetMembershipAsync(
                    teamId,
                    currentUserId,
                    cancellationToken);

        if (currentMembership is null)
        {
            return new AddTeamMemberResult(
                AddTeamMemberStatus.TeamNotFoundOrNotMember);
        }

        if (currentMembership.Role != TeamRole.Owner &&
            currentMembership.Role != TeamRole.Admin)
        {
            return new AddTeamMemberResult(
                AddTeamMemberStatus.Forbidden);
        }

        var user =
            await _userDirectoryRepository
                .GetByEmailAsync(
                    email.Trim(),
                    cancellationToken);

        if (user is null)
        {
            return new AddTeamMemberResult(
                AddTeamMemberStatus.UserNotFound);
        }

        var alreadyMember =
            await _teamMembershipRepository
                .IsMemberAsync(
                    teamId,
                    user.Id,
                    cancellationToken);

        if (alreadyMember)
        {
            return new AddTeamMemberResult(
                AddTeamMemberStatus.AlreadyMember);
        }

        var membership =
            TeamMember.Create(
                teamId,
                user.Id,
                TeamRole.Member);

        await _teamMembershipRepository.AddAsync(
            membership,
            cancellationToken);

        return new AddTeamMemberResult(
            AddTeamMemberStatus.Added,
            user.Id,
            user.Email,
            membership.Role);
    }
}