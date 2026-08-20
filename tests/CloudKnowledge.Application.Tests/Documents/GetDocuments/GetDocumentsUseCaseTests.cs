using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.GetDocuments;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Tests.Documents.GetDocuments;

public sealed class GetDocumentsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnPaginatedDocuments()
    {
        var documents = new[]
        {
            Document.Create("first.pdf", "application/pdf"),
            Document.Create("second.pdf", "application/pdf"),
            Document.Create("third.pdf", "application/pdf")
        };

        var repository = new FakeDocumentRepository(documents);
        var useCase = new GetDocumentsUseCase(repository);

        var result = await useCase.ExecuteAsync(
            page: 1,
            pageSize: 2,
            CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPageIsZero_ShouldThrow()
    {
        var repository = new FakeDocumentRepository([]);
        var useCase = new GetDocumentsUseCase(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            useCase.ExecuteAsync(
                page: 0,
                pageSize: 20,
                CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WhenPageSizeIsTooLarge_ShouldThrow()
    {
        var repository = new FakeDocumentRepository([]);
        var useCase = new GetDocumentsUseCase(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            useCase.ExecuteAsync(
                page: 1,
                pageSize: 101,
                CancellationToken.None));
    }

    private sealed class FakeDocumentRepository : IDocumentRepository
    {
        private readonly IReadOnlyList<Document> _documents;

        public FakeDocumentRepository(
            IReadOnlyList<Document> documents)
        {
            _documents = documents;
        }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<Document?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            var document = _documents
                .SingleOrDefault(document => document.Id == id);

            return Task.FromResult(document);
        }

        public Task<IReadOnlyList<Document>> GetPageAsync(
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Document> result = _documents
                .Skip(skip)
                .Take(take)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<int> CountAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_documents.Count);
        }
    }
}