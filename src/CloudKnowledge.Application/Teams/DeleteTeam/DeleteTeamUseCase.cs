using CloudKnowledge.Application.Documents.DeleteDocument;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Teams.DeleteTeam;

public sealed class DeleteTeamUseCase
{
    private readonly ITeamRepository _teamRepository;
    private readonly ITeamMembershipRepository _teamMembershipRepository;
    private readonly ITeamDeletionRepository _teamDeletionRepository;
    private readonly IDocumentDeletionStorage _documentStorage;
    private readonly ICurrentUser _currentUser;

    public DeleteTeamUseCase(
        ITeamRepository teamRepository,
        ITeamMembershipRepository teamMembershipRepository,
        ITeamDeletionRepository teamDeletionRepository,
        IDocumentDeletionStorage documentStorage,
        ICurrentUser currentUser)
    {
        _teamRepository = teamRepository;
        _teamMembershipRepository = teamMembershipRepository;
        _teamDeletionRepository = teamDeletionRepository;
        _documentStorage = documentStorage;
        _currentUser = currentUser;
    }

    public async Task<DeleteTeamStatus> ExecuteAsync(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var team =
            await _teamRepository.GetByIdAsync(
                teamId,
                cancellationToken);

        if (team is null)
        {
            return DeleteTeamStatus.NotFound;
        }

        var membership =
            await _teamMembershipRepository.GetMembershipAsync(
                teamId,
                userId,
                cancellationToken);

        if (membership is null)
        {
            return DeleteTeamStatus.NotFound;
        }

        if (membership.Role != TeamRole.Owner)
        {
            return DeleteTeamStatus.Forbidden;
        }

        if (await _teamDeletionRepository.HasChildrenAsync(
                teamId,
                cancellationToken))
        {
            return DeleteTeamStatus.HasChildren;
        }

        var ownedDocumentIds =
            await _teamDeletionRepository.GetOwnedDocumentIdsAsync(
                teamId,
                cancellationToken);

        await _teamDeletionRepository.DeleteLeafAsync(
            teamId,
            cancellationToken);

        foreach (var documentId in ownedDocumentIds)
        {
            await _documentStorage.DeleteAsync(
                documentId,
                cancellationToken);
        }

        return DeleteTeamStatus.Deleted;
    }
}
