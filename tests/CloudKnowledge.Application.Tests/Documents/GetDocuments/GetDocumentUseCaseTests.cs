using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.GetDocument;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Tests.Documents.GetDocument;

public sealed class GetDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDocumentExists_ShouldReturnDocument()
    {
        var document = Document.Create(
            "architecture.pdf",
            "application/pdf");

        var repository = new FakeDocumentRepository(document);

        var useCase = new GetDocumentUseCase(repository);

        var result = await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.NotNull(result);

        Assert.Equal(document.Id, result.Id);
        Assert.Equal(document.FileName, result.FileName);
        Assert.Equal(document.ContentType, result.ContentType);
        Assert.Equal(document.Status, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocumentDoesNotExist_ShouldReturnNull()
    {
        var repository = new FakeDocumentRepository(null);

        var useCase = new GetDocumentUseCase(repository);

        var result = await useCase.ExecuteAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class FakeDocumentRepository : IDocumentRepository
    {
        private readonly Document? _document;

        public FakeDocumentRepository(Document? document)
        {
            _document = document;
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
            if (_document?.Id == id)
            {
                return Task.FromResult<Document?>(_document);
            }

            return Task.FromResult<Document?>(null);
        }
    }
}