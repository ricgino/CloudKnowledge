using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents;

public interface IDocumentRepository
{
    Task AddAsync(
        Document document,
        CancellationToken cancellationToken);

    Task<Document?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}