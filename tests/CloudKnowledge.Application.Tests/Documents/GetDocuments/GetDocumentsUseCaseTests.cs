using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Application.Documents.GetDocuments;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Tests.Documents.GetDocuments;

public sealed class GetDocumentsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyAccessiblePaginatedDocuments()
    {
        var userId =
            Guid.NewGuid();

        var documents =
            new[]
            {
                Document.Create(
                    "first.pdf",
                    "application/pdf"),

                Document.Create(
                    "second.pdf",
                    "application/pdf"),

                Document.Create(
                    "third.pdf",
                    "application/pdf")
            };

        var repository =
            new FakeDocumentAccessRepository(
                documents);

        var useCase =
            new GetDocumentsUseCase(
                repository,
                new FakeCurrentUser(
                    userId));

        var result =
            await useCase.ExecuteAsync(
                page: 1,
                pageSize: 2,
                CancellationToken.None);

        Assert.Equal(
            1,
            result.Page);

        Assert.Equal(
            2,
            result.PageSize);

        Assert.Equal(
            3,
            result.TotalCount);

        Assert.Equal(
            2,
            result.TotalPages);

        Assert.Equal(
            2,
            result.Items.Count);

        Assert.Equal(
            userId,
            repository.ReceivedUserId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldKeepPersonalOwnershipSeparateFromDeleteCapability()
    {
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var personalDocument =
            Document.Create(
                "personal.pdf",
                "application/pdf");
        personalDocument.AssignUserOwner(userId);

        var teamDocument =
            Document.Create(
                "team.pdf",
                "application/pdf");
        teamDocument.AssignTeamOwner(teamId);

        var repository =
            new FakeDocumentAccessRepository(
                new[]
                {
                    personalDocument,
                    teamDocument
                },
                new[]
                {
                    teamDocument.Id
                });

        var useCase =
            new GetDocumentsUseCase(
                repository,
                new FakeCurrentUser(userId));

        var result =
            await useCase.ExecuteAsync(
                page: 1,
                pageSize: 20,
                CancellationToken.None);

        var personalItem =
            Assert.Single(
                result.Items,
                item => item.Id == personalDocument.Id);

        Assert.True(personalItem.IsOwner);
        Assert.True(personalItem.CanDelete);

        var teamItem =
            Assert.Single(
                result.Items,
                item => item.Id == teamDocument.Id);

        Assert.False(teamItem.IsOwner);
        Assert.True(teamItem.CanDelete);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPageIsZero_ShouldThrow()
    {
        var useCase =
            CreateUseCase();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () =>
                useCase.ExecuteAsync(
                    page: 0,
                    pageSize: 20,
                    CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WhenPageSizeIsTooLarge_ShouldThrow()
    {
        var useCase =
            CreateUseCase();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () =>
                useCase.ExecuteAsync(
                    page: 1,
                    pageSize: 101,
                    CancellationToken.None));
    }

    private static GetDocumentsUseCase CreateUseCase()
    {
        return new GetDocumentsUseCase(
            new FakeDocumentAccessRepository(
                Array.Empty<Document>()),
            new FakeCurrentUser(
                Guid.NewGuid()));
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
        private readonly IReadOnlyList<Document>
            _documents;

        private readonly IReadOnlyCollection<Guid>
            _teamOwnedDeletableDocumentIds;

        public FakeDocumentAccessRepository(
            IReadOnlyList<Document> documents,
            IReadOnlyCollection<Guid>? teamOwnedDeletableDocumentIds = null)
        {
            _documents =
                documents;

            _teamOwnedDeletableDocumentIds =
                teamOwnedDeletableDocumentIds ??
                Array.Empty<Guid>();
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

            var document =
                _documents.SingleOrDefault(
                    item =>
                        item.Id == documentId);

            return Task.FromResult(
                document);
        }

        public Task<IReadOnlyList<Document>> GetPageAsync(
            Guid userId,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            ReceivedUserId =
                userId;

            IReadOnlyList<Document> result =
                _documents
                    .Skip(skip)
                    .Take(take)
                    .ToList();

            return Task.FromResult(
                result);
        }

        public Task<int> CountAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            ReceivedUserId =
                userId;

            return Task.FromResult(
                _documents.Count);
        }

        public Task<bool> CanAccessAsync(
            Guid userId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _documents.Any(
                    document =>
                        document.Id == documentId));
        }

        public Task<IReadOnlyCollection<Guid>> GetTeamOwnedDeletableDocumentIdsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> documentIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<Guid> result =
                _teamOwnedDeletableDocumentIds
                    .Where(documentIds.Contains)
                    .ToArray();

            return Task.FromResult(result);
        }
    }
}
