using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.Access;

public interface IDocumentAccessRepository
{
    Task<bool> CanAccessAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken);

    Task<Document?> GetByIdAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Document>> GetPageAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<int> CountAsync(
        Guid userId,
        CancellationToken cancellationToken);
}