using CloudKnowledge.Application.Documents.DeleteDocument;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Teams.DeleteTeam;
using CloudKnowledge.Application.Teams.GetTeams;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Tests.Teams.DeleteTeam;

public sealed class DeleteTeamUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTeamDoesNotExist_ShouldReturnNotFound()
    {
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var deletion = new FakeTeamDeletionRepository();

        var useCase = CreateUseCase(
            userId,
            team: null,
            membership: null,
            deletion,
            new FakeDeletionStorage());

        var result = await useCase.ExecuteAsync(
            teamId,
            CancellationToken.None);

        Assert.Equal(DeleteTeamStatus.NotFound, result);
        Assert.False(deletion.DeleteCalled);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsNotDirectMember_ShouldReturnNotFound()
    {
        var userId = Guid.NewGuid();
        var team = Team.Create("Engineering");
        var deletion = new FakeTeamDeletionRepository();

        var useCase = CreateUseCase(
            userId,
            team,
            membership: null,
            deletion,
            new FakeDeletionStorage());

        var result = await useCase.ExecuteAsync(
            team.Id,
            CancellationToken.None);

        Assert.Equal(DeleteTeamStatus.NotFound, result);
        Assert.False(deletion.DeleteCalled);
    }

    [Theory]
    [InlineData(TeamRole.Member)]
    [InlineData(TeamRole.Admin)]
    public async Task ExecuteAsync_WhenDirectMemberIsNotOwner_ShouldReturnForbidden(
        TeamRole role)
    {
        var userId = Guid.NewGuid();
        var team = Team.Create("Engineering");
        var membership = TeamMember.Create(
            team.Id,
            userId,
            role);
        var deletion = new FakeTeamDeletionRepository();

        var useCase = CreateUseCase(
            userId,
            team,
            membership,
            deletion,
            new FakeDeletionStorage());

        var result = await useCase.ExecuteAsync(
            team.Id,
            CancellationToken.None);

        Assert.Equal(DeleteTeamStatus.Forbidden, result);
        Assert.False(deletion.DeleteCalled);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnedTeamHasChildren_ShouldReturnHasChildren()
    {
        var userId = Guid.NewGuid();
        var team = Team.Create("Engineering");
        var membership = TeamMember.Create(
            team.Id,
            userId,
            TeamRole.Owner);
        var deletion = new FakeTeamDeletionRepository
        {
            HasChildren = true
        };

        var useCase = CreateUseCase(
            userId,
            team,
            membership,
            deletion,
            new FakeDeletionStorage());

        var result = await useCase.ExecuteAsync(
            team.Id,
            CancellationToken.None);

        Assert.Equal(DeleteTeamStatus.HasChildren, result);
        Assert.False(deletion.DeleteCalled);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnerDeletesLeafTeam_ShouldDeleteDatabaseAndOwnedBlobs()
    {
        var userId = Guid.NewGuid();
        var team = Team.Create("Engineering");
        var membership = TeamMember.Create(
            team.Id,
            userId,
            TeamRole.Owner);

        var firstDocumentId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();

        var deletion = new FakeTeamDeletionRepository
        {
            OwnedDocumentIds =
                new[]
                {
                    firstDocumentId,
                    secondDocumentId
                }
        };

        var storage = new FakeDeletionStorage();

        var useCase = CreateUseCase(
            userId,
            team,
            membership,
            deletion,
            storage);

        var result = await useCase.ExecuteAsync(
            team.Id,
            CancellationToken.None);

        Assert.Equal(DeleteTeamStatus.Deleted, result);
        Assert.True(deletion.DeleteCalled);
        Assert.Equal(team.Id, deletion.DeletedTeamId);
        Assert.Equal(
            new[]
            {
                firstDocumentId,
                secondDocumentId
            },
            storage.DeletedDocumentIds);
    }

    private static DeleteTeamUseCase CreateUseCase(
        Guid userId,
        Team? team,
        TeamMember? membership,
        FakeTeamDeletionRepository deletion,
        FakeDeletionStorage storage)
    {
        return new DeleteTeamUseCase(
            new FakeTeamRepository(team),
            new FakeMembershipRepository(membership),
            deletion,
            storage,
            new FakeCurrentUser(userId));
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

    private sealed class FakeTeamRepository : ITeamRepository
    {
        private readonly Team? _team;

        public FakeTeamRepository(Team? team)
        {
            _team = team;
        }

        public Task<Team?> GetByIdAsync(
            Guid teamId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _team?.Id == teamId
                    ? _team
                    : null);
        }

        public Task AddAsync(
            Team team,
            TeamMember ownerMembership,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<GetTeamsResult>> GetForUserAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeMembershipRepository : ITeamMembershipRepository
    {
        private readonly TeamMember? _membership;

        public FakeMembershipRepository(TeamMember? membership)
        {
            _membership = membership;
        }

        public Task<TeamMember?> GetMembershipAsync(
            Guid teamId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _membership?.TeamId == teamId &&
                _membership.UserId == userId
                    ? _membership
                    : null);
        }

        public Task<bool> IsMemberAsync(
            Guid teamId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _membership?.TeamId == teamId &&
                _membership.UserId == userId);
        }

        public Task AddAsync(
            TeamMember membership,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeTeamDeletionRepository : ITeamDeletionRepository
    {
        public bool HasChildren { get; init; }
        public IReadOnlyList<Guid> OwnedDocumentIds { get; init; } =
            Array.Empty<Guid>();
        public bool DeleteCalled { get; private set; }
        public Guid? DeletedTeamId { get; private set; }

        public Task<bool> HasChildrenAsync(
            Guid teamId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(HasChildren);
        }

        public Task<IReadOnlyList<Guid>> GetOwnedDocumentIdsAsync(
            Guid teamId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(OwnedDocumentIds);
        }

        public Task DeleteLeafAsync(
            Guid teamId,
            CancellationToken cancellationToken)
        {
            DeleteCalled = true;
            DeletedTeamId = teamId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeletionStorage : IDocumentDeletionStorage
    {
        public List<Guid> DeletedDocumentIds { get; } = new();

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            DeletedDocumentIds.Add(documentId);
            return Task.CompletedTask;
        }
    }
}
