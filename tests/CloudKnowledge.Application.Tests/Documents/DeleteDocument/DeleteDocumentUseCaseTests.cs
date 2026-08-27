using CloudKnowledge.Application.Documents.DeleteDocument;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Tests.Documents.DeleteDocument;

public sealed class DeleteDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOwnedDocumentExists_ShouldDeleteDatabaseAndStorage()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var repository = new FakeDeletionRepository(true);
        var storage = new FakeDeletionStorage();
        var useCase = new DeleteDocumentUseCase(
            repository,
            storage,
            new FakeCurrentUser(userId));

        var deleted = await useCase.ExecuteAsync(
            documentId,
            CancellationToken.None);

        Assert.True(deleted);
        Assert.Equal(userId, repository.ReceivedOwnerUserId);
        Assert.Equal(documentId, repository.ReceivedDocumentId);
        Assert.Equal(documentId, storage.DeletedDocumentId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocumentIsNotOwned_ShouldNotDeleteStorage()
    {
        var repository = new FakeDeletionRepository(false);
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
        private readonly bool _deleted;

        public FakeDeletionRepository(bool deleted)
        {
            _deleted = deleted;
        }

        public Guid ReceivedOwnerUserId { get; private set; }
        public Guid ReceivedDocumentId { get; private set; }

        public Task<bool> DeleteOwnedAsync(
            Guid ownerUserId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            ReceivedOwnerUserId = ownerUserId;
            ReceivedDocumentId = documentId;
            return Task.FromResult(_deleted);
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
