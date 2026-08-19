using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Tests.Documents.CreateDocument;

public sealed class CreateDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCreatePendingDocumentAndPersistIt()
    {
        var repository = new FakeDocumentRepository();
        var useCase = new CreateDocumentUseCase(repository);

        var result = await useCase.ExecuteAsync(
            "architecture.pdf",
            "application/pdf",
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("architecture.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(DocumentStatus.Pending, result.Status);

        Assert.NotNull(repository.AddedDocument);
        Assert.Equal(result.Id, repository.AddedDocument.Id);
    }

    private sealed class FakeDocumentRepository : IDocumentRepository
    {
        public Document? AddedDocument { get; private set; }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            AddedDocument = document;

            return Task.CompletedTask;
        }

        public Task<Document?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Document?>(null);
        }
    }
}