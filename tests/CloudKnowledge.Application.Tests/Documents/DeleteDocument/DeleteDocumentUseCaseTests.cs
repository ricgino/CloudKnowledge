using CloudKnowledge.Application.Documents.DeleteDocument;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Tests.Documents.DeleteDocument;

public sealed class DeleteDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDeletionIsAuthorized_ShouldDeleteDatabaseAndStorage()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var repository = new FakeDeletionRepository(
            authorizedDeleted: true,
            legacyOwnedDeleted: false);
        var storage = new FakeDeletionStorage();
        var useCase = new DeleteDocumentUseCase(
            repository,
            storage,
            new FakeCurrentUser(userId));

        var deleted = await useCase.ExecuteAsync(
            documentId,
            CancellationToken.None);

        Assert.True(deleted);
        Assert.True(repository.AuthorizedDeleteCalled);
        Assert.Equal(userId, repository.ReceivedUserId);
        Assert.Equal(documentId, repository.ReceivedDocumentId);
        Assert.Equal(documentId, storage.DeletedDocumentId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeletionIsNotAuthorized_ShouldNotDeleteStorage()
    {
        var repository = new FakeDeletionRepository(
            authorizedDeleted: false,
            legacyOwnedDeleted: false);
        var storage = new FakeDeletionStorage();
        var useCase = new DeleteDocumentUseCase(
            repository,
            storage,
            new FakeCurrentUser(Guid.NewGuid()));

        var deleted = await useCase.ExecuteAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(deleted);
        Assert.Null(storage.DeletedDocumentId);
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

    private sealed class FakeDeletionRepository : IDocumentDeletionRepository
    {
        private readonly bool _authorizedDeleted;
        private readonly bool _legacyOwnedDeleted;

        public FakeDeletionRepository(
            bool authorizedDeleted,
            bool legacyOwnedDeleted)
        {
            _authorizedDeleted = authorizedDeleted;
            _legacyOwnedDeleted = legacyOwnedDeleted;
        }

        public bool AuthorizedDeleteCalled { get; private set; }
        public Guid ReceivedUserId { get; private set; }
        public Guid ReceivedDocumentId { get; private set; }

        public Task<bool> DeleteOwnedAsync(
            Guid ownerUserId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            ReceivedUserId = ownerUserId;
            ReceivedDocumentId = documentId;
            return Task.FromResult(_legacyOwnedDeleted);
        }

        public Task<bool> DeleteAuthorizedAsync(
            Guid userId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            AuthorizedDeleteCalled = true;
            ReceivedUserId = userId;
            ReceivedDocumentId = documentId;
            return Task.FromResult(_authorizedDeleted);
        }
    }

    private sealed class FakeDeletionStorage : IDocumentDeletionStorage
    {
        public Guid? DeletedDocumentId { get; private set; }

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            DeletedDocumentId = documentId;
            return Task.CompletedTask;
        }
    }
}
