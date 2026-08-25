using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Application.Documents.GetDocument;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Tests.Documents.GetDocument;

public sealed class GetDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDocumentIsAccessible_ShouldReturnDocument()
    {
        var userId =
            Guid.NewGuid();

        var document =
            Document.Create(
                "architecture.pdf",
                "application/pdf");

        var repository =
            new FakeDocumentAccessRepository(
                document);

        var useCase =
            new GetDocumentUseCase(
                repository,
                new FakeCurrentUser(
                    userId));

        var result =
            await useCase.ExecuteAsync(
                document.Id,
                CancellationToken.None);

        Assert.NotNull(
            result);

        Assert.Equal(
            document.Id,
            result.Id);

        Assert.Equal(
            document.FileName,
            result.FileName);

        Assert.Equal(
            document.ContentType,
            result.ContentType);

        Assert.Equal(
            document.Status,
            result.Status);

        Assert.Equal(
            userId,
            repository.ReceivedUserId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocumentIsNotAccessible_ShouldReturnNull()
    {
        var repository =
            new FakeDocumentAccessRepository(
                null);

        var useCase =
            new GetDocumentUseCase(
                repository,
                new FakeCurrentUser(
                    Guid.NewGuid()));

        var result =
            await useCase.ExecuteAsync(
                Guid.NewGuid(),
                CancellationToken.None);

        Assert.Null(
            result);
    }

    private sealed class FakeCurrentUser
        : ICurrentUser
    {
        private readonly Guid
            _userId;

        public FakeCurrentUser(
            Guid userId)
        {
            _userId =
                userId;
        }

        public Task<Guid> GetUserIdAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _userId);
        }
    }

    private sealed class FakeDocumentAccessRepository
        : IDocumentAccessRepository
    {
        private readonly Document?
            _document;

        public FakeDocumentAccessRepository(
            Document? document)
        {
            _document =
                document;
        }

        public Guid ReceivedUserId
        {
            get;
            private set;
        }

        public Task<Document?> GetByIdAsync(
            Guid userId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            ReceivedUserId =
                userId;

            if (_document?.Id ==
                documentId)
            {
                return Task.FromResult<Document?>(
                    _document);
            }

            return Task.FromResult<Document?>(
                null);
        }

        public Task<bool> CanAccessAsync(
            Guid userId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                false);
        }

        public Task<IReadOnlyList<Document>> GetPageAsync(
            Guid userId,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Document>>(
                Array.Empty<Document>());
        }

        public Task<int> CountAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                0);
        }
    }
}