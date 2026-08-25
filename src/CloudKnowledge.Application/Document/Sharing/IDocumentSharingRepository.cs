using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.Sharing;

public interface IDocumentSharingRepository
{
    Task<bool> IsOwnedByAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken);

    Task<bool> IsSharedWithTeamAsync(
        Guid documentId,
        Guid teamId,
        CancellationToken cancellationToken);

    Task AddAsync(
        DocumentTeamAccess access,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        Guid documentId,
        Guid teamId,
        CancellationToken cancellationToken);
}