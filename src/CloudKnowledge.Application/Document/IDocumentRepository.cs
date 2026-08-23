using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents;

public interface IDocumentRepository
{
    Task AddAsync(
        Document document,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        Document document,
        CancellationToken cancellationToken);        

    Task<Document?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Document>> GetPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<int> CountAsync(
        CancellationToken cancellationToken);

}