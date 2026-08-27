using CloudKnowledge.Application.Notifications.DocumentReady;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.Sharing;

public sealed class ShareDocumentWithTeamUseCase
{
    private readonly IDocumentSharingRepository _documentSharingRepository;
    private readonly ITeamMembershipRepository _teamMembershipRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentReadyPublisher _documentReadyPublisher;
    private readonly ICurrentUser _currentUser;

    public ShareDocumentWithTeamUseCase(
        IDocumentSharingRepository documentSharingRepository,
        ITeamMembershipRepository teamMembershipRepository,
        IDocumentRepository documentRepository,
        IDocumentReadyPublisher documentReadyPublisher,
        ICurrentUser currentUser)
    {
        _documentSharingRepository = documentSharingRepository;
        _teamMembershipRepository = teamMembershipRepository;
        _documentRepository = documentRepository;
        _documentReadyPublisher = documentReadyPublisher;
        _currentUser = currentUser;
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
            await _documentSharingRepository.IsOwnedByAsync(
                userId,
                documentId,
                cancellationToken);

        if (!ownsDocument)
        {
            return ShareDocumentStatus
                .DocumentNotFoundOrNotOwner;
        }

        var isTeamMember =
            await _teamMembershipRepository.IsMemberAsync(
                teamId,
                userId,
                cancellationToken);

        if (!isTeamMember)
        {
            return ShareDocumentStatus
                .TeamNotFoundOrNotMember;
        }

        var alreadyShared =
            await _documentSharingRepository.IsSharedWithTeamAsync(
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

        var document =
            await _documentRepository.GetByIdAsync(
                documentId,
                cancellationToken);

        if (document?.Status ==
            DocumentStatus.Ready)
        {
            await _documentReadyPublisher.PublishAsync(
                documentId,
                cancellationToken);
        }

        return ShareDocumentStatus.Shared;
    }
}
