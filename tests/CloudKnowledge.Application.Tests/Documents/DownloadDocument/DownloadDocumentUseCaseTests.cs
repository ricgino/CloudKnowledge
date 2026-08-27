using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Application.Documents.DownloadDocument;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Tests.Documents.DownloadDocument;

public sealed class DownloadDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDocumentIsAccessible_ShouldReturnStoredContent()
    {
        var userId = Guid.NewGuid();
        var document = Document.Create(
            "manual.pdf",
            "application/pdf");
        var repository = new FakeAccessRepository(document);
        var storage = new FakeDocumentStorage();
        var useCase = new DownloadDocumentUseCase(
            repository,
            storage,
            new FakeCurrentUser(userId));

        var result = await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(document.FileName, result.FileName);
        Assert.Equal(document.ContentType, result.ContentType);
        Assert.Equal(userId, repository.ReceivedUserId);
        Assert.Equal(document.Id, storage.OpenedDocumentId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocumentIsNotAccessible_ShouldReturnNullWithoutOpeningStorage()
    {
        var storage = new FakeDocumentStorage();
        var useCase = new DownloadDocumentUseCase(
            new FakeAccessRepository(null),
            storage,
            new FakeCurrentUser(Guid.NewGuid()));

        var result = await useCase.ExecuteAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Null(storage.OpenedDocumentId);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        private readonly Guid _userId;

        public FakeCurrentUser(Guid userId)
        {
            _userId = userId;
        }

        public Task<Guid> GetUserIdAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(_userId);
    }

    private sealed class FakeAccessRepository : IDocumentAccessRepository
    {
        private readonly Document? _document;

        public FakeAccessRepository(Document? document)
        {
            _document = document;
        }

        public Guid ReceivedUserId { get; private set; }

        public Task<Document?> GetByIdAsync(
            Guid userId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            ReceivedUserId = userId;
            return Task.FromResult(
                _document?.Id == documentId
                    ? _document
                    : null);
        }

        public Task<bool> CanAccessAsync(
            Guid userId,
            Guid documentId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<Document>> GetPageAsync(
            Guid userId,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Document>>(
                Array.Empty<Document>());

        public Task<int> CountAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(0);
    }

    private sealed class FakeDocumentStorage : IDocumentStorage
    {
        public Guid? OpenedDocumentId { get; private set; }

        public Task UploadAsync(
            Guid documentId,
            Stream content,
            string contentType,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<Stream> OpenReadAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            OpenedDocumentId = documentId;
            Stream content = new MemoryStream([1, 2, 3]);
            return Task.FromResult(content);
        }
    }
}
