using System.Collections.Concurrent;
using CloudKnowledge.Application.Documents;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, Document> _documents = new();

    public Task AddAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_documents.TryAdd(document.Id, document))
        {
            throw new InvalidOperationException(
                $"Document '{document.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<Document?> GetByIdAsync(
    Guid id,
    CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _documents.TryGetValue(id, out var document);

        return Task.FromResult(document);
    }
}