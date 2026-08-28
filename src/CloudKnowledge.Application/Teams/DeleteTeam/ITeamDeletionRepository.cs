namespace CloudKnowledge.Application.Teams.DeleteTeam;

public interface ITeamDeletionRepository
{
    Task<bool> HasChildrenAsync(
        Guid teamId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetOwnedDocumentIdsAsync(
        Guid teamId,
        CancellationToken cancellationToken);

    Task DeleteLeafAsync(
        Guid teamId,
        CancellationToken cancellationToken);
}
