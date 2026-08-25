using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.Sharing;

public sealed class ShareDocumentWithTeamUseCase
{
    private readonly IDocumentSharingRepository
        _documentSharingRepository;

    private readonly ITeamMembershipRepository
        _teamMembershipRepository;

    private readonly ICurrentUser
        _currentUser;

    public ShareDocumentWithTeamUseCase(
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

    public async Task<ShareDocumentStatus> ExecuteAsync(
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
            return ShareDocumentStatus
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
            return ShareDocumentStatus
                .TeamNotFoundOrNotMember;
        }

        var alreadyShared =
            await _documentSharingRepository
                .IsSharedWithTeamAsync(
                    documentId,
                    teamId,
                    cancellationToken);

        if (alreadyShared)
        {
            return ShareDocumentStatus
                .AlreadyShared;
        }

        var access =
            DocumentTeamAccess.Create(
                documentId,
                teamId);

        await _documentSharingRepository.AddAsync(
            access,
            cancellationToken);

        return ShareDocumentStatus.Shared;
    }
}