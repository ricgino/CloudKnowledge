using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.ProcessDocument;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Tests.Documents.ProcessDocument;

public sealed class ProcessDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldMoveDocumentFromPendingToReady()
    {
        var document =
            Document.Create(
                "architecture.pdf",
                "application/pdf");

        var repository =
            new FakeDocumentRepository(document);

        var useCase =
            new ProcessDocumentUseCase(repository);

        await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.Equal(
            DocumentStatus.Ready,
            document.Status);

        Assert.Equal(
            2,
            repository.UpdateCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocumentDoesNotExist_ShouldThrow()
    {
        var repository =
            new FakeDocumentRepository(null);

        var useCase =
            new ProcessDocumentUseCase(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(
                Guid.NewGuid(),
                CancellationToken.None));
    }

    private sealed class FakeDocumentRepository
        : IDocumentRepository
    {
        private readonly Document? _document;

        public int UpdateCount { get; private set; }

        public FakeDocumentRepository(
            Document? document)
        {
            _document = document;
        }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            UpdateCount++;

            return Task.CompletedTask;
        }

        public Task<Document?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            if (_document?.Id == id)
            {
                return Task.FromResult<Document?>(
                    _document);
            }

            return Task.FromResult<Document?>(null);
        }

        public Task<IReadOnlyList<Document>> GetPageAsync(
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Document>>(
                Array.Empty<Document>());
        }

        public Task<int> CountAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }
}