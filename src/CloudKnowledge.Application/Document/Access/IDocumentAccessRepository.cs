using CloudKnowledge.Application.Documents.GetDocuments;
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

    Task<IReadOnlyList<Document>> GetPageAsync(
        Guid userId,
        int skip,
        int take,
        GetDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        return GetPageAsync(
            userId,
            skip,
            take,
            cancellationToken);
    }

    Task<int> CountAsync(
        Guid userId,
        GetDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        return CountAsync(
            userId,
            cancellationToken);
    }

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<DocumentAccessTeamResult>>>
        GetVisibleTeamAccessAsync(
            Guid userId,
            IReadOnlyCollection<Guid> documentIds,
            CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, IReadOnlyList<DocumentAccessTeamResult>> result =
            documentIds
                .Distinct()
                .ToDictionary(
                    documentId => documentId,
                    _ =>
                        (IReadOnlyList<DocumentAccessTeamResult>)
                        Array.Empty<DocumentAccessTeamResult>());

        return Task.FromResult(
            result);
    }

    Task<IReadOnlyCollection<Guid>> GetTeamOwnedDeletableDocumentIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<Guid>>(
            Array.Empty<Guid>());
    }
}
