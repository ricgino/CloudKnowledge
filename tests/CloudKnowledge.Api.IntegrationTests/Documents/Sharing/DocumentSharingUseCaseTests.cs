using CloudKnowledge.Application.Documents.Sharing;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Tests.Documents.Sharing;

public sealed class DocumentSharingUseCaseTests
{
    [Fact]
    public async Task Share_WhenOwnerAndTeamMember_ShouldShare()
    {
        var fixture =
            new Fixture();

        var result =
            await fixture.ShareUseCase.ExecuteAsync(
                fixture.DocumentId,
                fixture.TeamId,
                CancellationToken.None);

        Assert.Equal(
            ShareDocumentStatus.Shared,
            result);

        Assert.NotNull(
            fixture.SharingRepository.AddedAccess);
    }

    [Fact]
    public async Task Share_WhenNotOwner_ShouldFail()
    {
        var fixture =
            new Fixture(
                ownsDocument: false);

        var result =
            await fixture.ShareUseCase.ExecuteAsync(
                fixture.DocumentId,
                fixture.TeamId,
                CancellationToken.None);

        Assert.Equal(
            ShareDocumentStatus.DocumentNotFoundOrNotOwner,
            result);
    }

    [Fact]
    public async Task Share_WhenNotTeamMember_ShouldFail()
    {
        var fixture =
            new Fixture(
                isTeamMember: false);

        var result =
            await fixture.ShareUseCase.ExecuteAsync(
                fixture.DocumentId,
                fixture.TeamId,
                CancellationToken.None);

        Assert.Equal(
            ShareDocumentStatus.TeamNotFoundOrNotMember,
            result);
    }

    [Fact]
    public async Task Share_WhenAlreadyShared_ShouldBeIdempotent()
    {
        var fixture =
            new Fixture(
                isShared: true);

        var result =
            await fixture.ShareUseCase.ExecuteAsync(
                fixture.DocumentId,
                fixture.TeamId,
                CancellationToken.None);

        Assert.Equal(
            ShareDocumentStatus.AlreadyShared,
            result);

        Assert.Null(
            fixture.SharingRepository.AddedAccess);
    }

    [Fact]
    public async Task Unshare_WhenOwnerAndTeamMember_ShouldRemove()
    {
        var fixture =
            new Fixture(
                isShared: true);

        var result =
            await fixture.UnshareUseCase.ExecuteAsync(
                fixture.DocumentId,
                fixture.TeamId,
                CancellationToken.None);

        Assert.Equal(
            UnshareDocumentStatus.Unshared,
            result);

        Assert.True(
            fixture.SharingRepository.Removed);
    }

    [Fact]
    public async Task Unshare_WhenNotOwner_ShouldFail()
    {
        var fixture =
            new Fixture(
                ownsDocument: false,
                isShared: true);

        var result =
            await fixture.UnshareUseCase.ExecuteAsync(
                fixture.DocumentId,
                fixture.TeamId,
                CancellationToken.None);

        Assert.Equal(
            UnshareDocumentStatus.DocumentNotFoundOrNotOwner,
            result);
    }

    [Fact]
    public async Task Unshare_WhenNotTeamMember_ShouldFail()
    {
        var fixture =
            new Fixture(
                isTeamMember: false,
                isShared: true);

        var result =
            await fixture.UnshareUseCase.ExecuteAsync(
                fixture.DocumentId,
                fixture.TeamId,
                CancellationToken.None);

        Assert.Equal(
            UnshareDocumentStatus.TeamNotFoundOrNotMember,
            result);
    }

    [Fact]
    public async Task Unshare_WhenNotShared_ShouldBeIdempotent()
    {
        var fixture =
            new Fixture(
                isShared: false);

        var result =
            await fixture.UnshareUseCase.ExecuteAsync(
                fixture.DocumentId,
                fixture.TeamId,
                CancellationToken.None);

        Assert.Equal(
            UnshareDocumentStatus.NotShared,
            result);

        Assert.False(
            fixture.SharingRepository.Removed);
    }

    private sealed class Fixture
    {
        public Guid UserId
        {
            get;
        } = Guid.NewGuid();

        public Guid DocumentId
        {
            get;
        } = Guid.NewGuid();

        public Guid TeamId
        {
            get;
        } = Guid.NewGuid();

        public FakeDocumentSharingRepository
            SharingRepository
        {
            get;
        }

        public ShareDocumentWithTeamUseCase
            ShareUseCase
        {
            get;
        }

        public UnshareDocumentFromTeamUseCase
            UnshareUseCase
        {
            get;
        }

        public Fixture(
            bool ownsDocument = true,
            bool isTeamMember = true,
            bool isShared = false)
        {
            SharingRepository =
                new FakeDocumentSharingRepository(
                    ownsDocument,
                    isShared);

            var membershipRepository =
                new FakeTeamMembershipRepository(
                    isTeamMember);

            var currentUser =
                new FakeCurrentUser(
                    UserId);

            ShareUseCase =
                new ShareDocumentWithTeamUseCase(
                    SharingRepository,
                    membershipRepository,
                    currentUser);

            UnshareUseCase =
                new UnshareDocumentFromTeamUseCase(
                    SharingRepository,
                    membershipRepository,
                    currentUser);
        }
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

    private sealed class FakeTeamMembershipRepository
        : ITeamMembershipRepository
    {
        private readonly bool
            _isMember;

        public FakeTeamMembershipRepository(
            bool isMember)
        {
            _isMember =
                isMember;
        }

        public Task<bool> IsMemberAsync(
            Guid teamId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _isMember);
        }

        public Task<TeamMember?> GetMembershipAsync(
            Guid teamId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<TeamMember?>(
                null);
        }

        public Task AddAsync(
            TeamMember membership,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDocumentSharingRepository
        : IDocumentSharingRepository
    {
        private readonly bool
            _ownsDocument;

        private bool
            _isShared;

        public FakeDocumentSharingRepository(
            bool ownsDocument,
            bool isShared)
        {
            _ownsDocument =
                ownsDocument;

            _isShared =
                isShared;
        }

        public DocumentTeamAccess? AddedAccess
        {
            get;
            private set;
        }

        public bool Removed
        {
            get;
            private set;
        }

        public Task<bool> IsOwnedByAsync(
            Guid userId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _ownsDocument);
        }

        public Task<bool> IsSharedWithTeamAsync(
            Guid documentId,
            Guid teamId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _isShared);
        }

        public Task AddAsync(
            DocumentTeamAccess access,
            CancellationToken cancellationToken)
        {
            AddedAccess =
                access;

            _isShared =
                true;

            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            Guid documentId,
            Guid teamId,
            CancellationToken cancellationToken)
        {
            Removed =
                true;

            _isShared =
                false;

            return Task.CompletedTask;
        }
    }
}