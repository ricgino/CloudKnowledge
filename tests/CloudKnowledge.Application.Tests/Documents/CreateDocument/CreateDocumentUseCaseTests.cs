using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Tests.Documents.CreateDocument;

public sealed class CreateDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithoutTeam_ShouldAssignCurrentUserAsOwner()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeDocumentRepository();
        var storage = new FakeDocumentStorage();
        var queue = new FakeDocumentProcessingQueue();
        var memberships = new FakeTeamMembershipRepository(isMember: false);

        var useCase = new CreateDocumentUseCase(
            repository,
            storage,
            queue,
            memberships,
            new FakeCurrentUser(userId));

        await using var content =
            new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var result = await useCase.ExecuteAsync(
            "architecture.pdf",
            "application/pdf",
            content,
            teamId: null,
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("architecture.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(DocumentStatus.Pending, result.Status);

        Assert.NotNull(repository.AddedDocument);
        Assert.Equal(userId, repository.AddedDocument.OwnerUserId);
        Assert.Null(repository.AddedDocument.OwnerTeamId);
        Assert.Equal(result.Id, repository.AddedDocument.Id);
        Assert.Equal(result.Id, storage.UploadedDocumentId);
        Assert.Equal("application/pdf", storage.UploadedContentType);
        Assert.Equal(4, storage.UploadedLength);
        Assert.Equal(result.Id, queue.PublishedDocumentId);
        Assert.Null(memberships.LastCheckedTeamId);
    }

    [Fact]
    public async Task ExecuteAsync_WithTeam_ShouldAssignTeamAsExclusiveOwner()
    {
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var repository = new FakeDocumentRepository();
        var storage = new FakeDocumentStorage();
        var queue = new FakeDocumentProcessingQueue();
        var memberships = new FakeTeamMembershipRepository(isMember: true);

        var useCase = new CreateDocumentUseCase(
            repository,
            storage,
            queue,
            memberships,
            new FakeCurrentUser(userId));

        await using var content =
            new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await useCase.ExecuteAsync(
            "team-guide.pdf",
            "application/pdf",
            content,
            teamId,
            CancellationToken.None);

        Assert.NotNull(repository.AddedDocument);
        Assert.Null(repository.AddedDocument.OwnerUserId);
        Assert.Equal(teamId, repository.AddedDocument.OwnerTeamId);
        Assert.Equal(teamId, memberships.LastCheckedTeamId);
        Assert.Equal(userId, memberships.LastCheckedUserId);
        Assert.Equal(result.Id, storage.UploadedDocumentId);
        Assert.Equal(result.Id, queue.PublishedDocumentId);
    }

    [Fact]
    public async Task ExecuteAsync_WithTeam_WhenUserIsNotDirectMember_ShouldRejectBeforeUpload()
    {
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var repository = new FakeDocumentRepository();
        var storage = new FakeDocumentStorage();
        var queue = new FakeDocumentProcessingQueue();
        var memberships = new FakeTeamMembershipRepository(isMember: false);

        var useCase = new CreateDocumentUseCase(
            repository,
            storage,
            queue,
            memberships,
            new FakeCurrentUser(userId));

        await using var content =
            new MemoryStream(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => useCase.ExecuteAsync(
                "private-team-guide.pdf",
                "application/pdf",
                content,
                teamId,
                CancellationToken.None));

        Assert.Null(repository.AddedDocument);
        Assert.Null(storage.UploadedDocumentId);
        Assert.Null(queue.PublishedDocumentId);
        Assert.Equal(teamId, memberships.LastCheckedTeamId);
        Assert.Equal(userId, memberships.LastCheckedUserId);
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

    private sealed class FakeCurrentUser : ICurrentUser
    {
        private readonly Guid _userId;

        public FakeCurrentUser(Guid userId)
        {
            _userId = userId;
        }

        public Task<Guid> GetUserIdAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_userId);
        }
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
    }

    private sealed class FakeDocumentProcessingQueue : IDocumentProcessingQueue
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

    private sealed class FakeTeamMembershipRepository : ITeamMembershipRepository
    {
        private readonly bool _isMember;

        public FakeTeamMembershipRepository(bool isMember)
        {
            _isMember = isMember;
        }

        public Guid? LastCheckedTeamId { get; private set; }
        public Guid? LastCheckedUserId { get; private set; }

        public Task<TeamMember?> GetMembershipAsync(
            Guid teamId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException(
                "This test only verifies direct membership checks.");
        }

        public Task<bool> IsMemberAsync(
            Guid teamId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            LastCheckedTeamId = teamId;
            LastCheckedUserId = userId;
            return Task.FromResult(_isMember);
        }

        public Task AddAsync(
            TeamMember membership,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException(
                "This test does not add team memberships.");
        }
    }
}
