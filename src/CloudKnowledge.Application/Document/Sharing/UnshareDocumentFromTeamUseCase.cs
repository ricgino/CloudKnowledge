using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Documents.Sharing;

public sealed class UnshareDocumentFromTeamUseCase
{
    private readonly IDocumentSharingRepository
        _documentSharingRepository;

    private readonly ITeamMembershipRepository
        _teamMembershipRepository;

    private readonly ICurrentUser
        _currentUser;

    public UnshareDocumentFromTeamUseCase(
        IDocumentSharingRepository documentSharingRepository,
        ITeamMembershipRepository teamMembershipRepository,
        ICurrentUser currentUser)
    {
        _documentSharingRepository =
            documentSharingRepository;

        _teamMembershipRepository =
            teamMembershipRepository;

        _currentUser =
            currentUser;
    }

    public async Task<UnshareDocumentStatus> ExecuteAsync(
        Guid documentId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var ownsDocument =
            await _documentSharingRepository
                .IsOwnedByAsync(
                    userId,
                    documentId,
                    cancellationToken);

        if (!ownsDocument)
        {
            return UnshareDocumentStatus
                .DocumentNotFoundOrNotOwner;
        }

        var isTeamMember =
            await _teamMembershipRepository
                .IsMemberAsync(
                    teamId,
                    userId,
                    cancellationToken);

        if (!isTeamMember)
        {
            return UnshareDocumentStatus
                .TeamNotFoundOrNotMember;
        }

        var isShared =
            await _documentSharingRepository
                .IsSharedWithTeamAsync(
                    documentId,
                    teamId,
                    cancellationToken);

        if (!isShared)
        {
            return UnshareDocumentStatus.NotShared;
        }

        await _documentSharingRepository.RemoveAsync(
            documentId,
            teamId,
            cancellationToken);

        return UnshareDocumentStatus.Unshared;
    }
}