using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.FailDocument;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Tests.Documents.FailDocument;

public sealed class FailDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenProcessing_ShouldMarkDocumentAsFailed()
    {
        var document =
            Document.Create(
                "architecture.pdf",
                "application/pdf");

        document.MarkAsProcessing();

        var repository =
            new FakeDocumentRepository(document);

        var useCase =
            new FailDocumentUseCase(repository);

        await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.Equal(
            DocumentStatus.Failed,
            document.Status);

        Assert.Equal(
            1,
            repository.UpdateCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPending_ShouldMarkDocumentAsFailed()
    {
        var document =
            Document.Create(
                "architecture.pdf",
                "application/pdf");

        var repository =
            new FakeDocumentRepository(document);

        var useCase =
            new FailDocumentUseCase(repository);

        await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.Equal(
            DocumentStatus.Failed,
            document.Status);

        Assert.Equal(
            1,
            repository.UpdateCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyFailed_ShouldDoNothing()
    {
        var document =
            Document.Create(
                "architecture.pdf",
                "application/pdf");

        document.MarkAsProcessing();
        document.MarkAsFailed();

        var repository =
            new FakeDocumentRepository(document);

        var useCase =
            new FailDocumentUseCase(repository);

        await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.Equal(
            DocumentStatus.Failed,
            document.Status);

        Assert.Equal(
            0,
            repository.UpdateCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyReady_ShouldDoNothing()
    {
        var document =
            Document.Create(
                "architecture.pdf",
                "application/pdf");

        document.MarkAsProcessing();
        document.MarkAsReady();

        var repository =
            new FakeDocumentRepository(document);

        var useCase =
            new FailDocumentUseCase(repository);

        await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.Equal(
            DocumentStatus.Ready,
            document.Status);

        Assert.Equal(
            0,
            repository.UpdateCount);
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
