using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Domain.Documents;


namespace CloudKnowledge.Application.Tests.Documents.CreateDocument;

public sealed class CreateDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldUploadAndPersistPendingDocument()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeDocumentStorage();
        var queue = new FakeDocumentProcessingQueue();

        var useCase = new CreateDocumentUseCase(
            repository,
            storage,
            queue);
            
        await using var content =
            new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var result = await useCase.ExecuteAsync(
            "architecture.pdf",
            "application/pdf",
            content,
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("architecture.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(DocumentStatus.Pending, result.Status);

        Assert.NotNull(repository.AddedDocument);
        Assert.Equal(
            result.Id,
            repository.AddedDocument.Id);

        Assert.Equal(
            result.Id,
            storage.UploadedDocumentId);

        Assert.Equal(
            "application/pdf",
            storage.UploadedContentType);

        Assert.Equal(
            4,
            storage.UploadedLength);
        Assert.Equal(
            result.Id,
            queue.PublishedDocumentId);
    }

    private sealed class FakeDocumentStorage : IDocumentStorage
    {
        public Guid? UploadedDocumentId { get; private set; }
        public string? UploadedContentType { get; private set; }
        public long UploadedLength { get; private set; }

        public async Task UploadAsync(
            Guid documentId,
            Stream content,
            string contentType,
            CancellationToken cancellationToken)
        {
            UploadedDocumentId = documentId;
            UploadedContentType = contentType;

            using var copy = new MemoryStream();

            await content.CopyToAsync(
                copy,
                cancellationToken);

            UploadedLength = copy.Length;
        }
        public Task<Stream> OpenReadAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException(
                "This test only verifies document upload.");
        }
    }

    private sealed class FakeDocumentRepository
        : IDocumentRepository
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

        public Task UpdateAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        public Task<Stream> OpenReadAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException(
                "This test only verifies document upload.");
        }
    }

    private sealed class FakeDocumentProcessingQueue
    : IDocumentProcessingQueue
{
    public Guid? PublishedDocumentId { get; private set; }

    public Task PublishAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        PublishedDocumentId = documentId;

        return Task.CompletedTask;
    }
}
}