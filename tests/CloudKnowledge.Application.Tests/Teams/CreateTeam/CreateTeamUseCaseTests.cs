using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Teams.CreateTeam;
using CloudKnowledge.Application.Teams.GetTeams;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Tests.Teams.CreateTeam;

public sealed class CreateTeamUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCreateRootTeamWithCurrentUserAsOwner()
    {
        var currentUserId =
            Guid.NewGuid();

        var repository =
            new FakeTeamRepository();

        var membershipRepository =
            new FakeTeamMembershipRepository();

        var useCase =
            CreateUseCase(
                repository,
                membershipRepository,
                currentUserId);

        var result =
            await useCase.ExecuteAsync(
                "  Engineering  ",
                null,
                CancellationToken.None);

        Assert.Equal(
            CreateTeamStatus.Created,
            result.Status);

        Assert.NotNull(
            repository.AddedTeam);

        Assert.Null(
            repository.AddedTeam.ParentTeamId);

        Assert.Equal(
            "Engineering",
            repository.AddedTeam.Name);

        Assert.NotNull(
            repository.AddedMembership);

        Assert.Equal(
            repository.AddedTeam.Id,
            repository.AddedMembership.TeamId);

        Assert.Equal(
            currentUserId,
            repository.AddedMembership.UserId);

        Assert.Equal(
            TeamRole.Owner,
            repository.AddedMembership.Role);

        Assert.Equal(
            repository.AddedTeam.Id,
            result.Id);

        Assert.Equal(
            "Engineering",
            result.Name);

        Assert.Null(
            result.ParentTeamId);

        Assert.Equal(
            TeamRole.Owner,
            result.Role);
    }

    [Theory]
    [InlineData(TeamRole.Owner)]
    [InlineData(TeamRole.Admin)]
    public async Task ExecuteAsync_ShouldCreateChildTeamForParentManagers(
        TeamRole parentRole)
    {
        var currentUserId =
            Guid.NewGuid();

        var parent =
            Team.Create(
                "Rai");

        var repository =
            new FakeTeamRepository
            {
                ExistingTeam = parent
            };

        var membershipRepository =
            new FakeTeamMembershipRepository
            {
                Membership =
                    TeamMember.Create(
                        parent.Id,
                        currentUserId,
                        parentRole)
            };

        var useCase =
            CreateUseCase(
                repository,
                membershipRepository,
                currentUserId);

        var result =
            await useCase.ExecuteAsync(
                "DeskSharing",
                parent.Id,
                CancellationToken.None);

        Assert.Equal(
            CreateTeamStatus.Created,
            result.Status);

        Assert.NotNull(
            repository.AddedTeam);

        Assert.Equal(
            parent.Id,
            repository.AddedTeam.ParentTeamId);

        Assert.Equal(
            parent.Id,
            result.ParentTeamId);

        Assert.NotNull(
            repository.AddedMembership);

        Assert.Equal(
            currentUserId,
            repository.AddedMembership.UserId);

        Assert.Equal(
            TeamRole.Owner,
            repository.AddedMembership.Role);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectChildCreationForParentMember()
    {
        var currentUserId =
            Guid.NewGuid();

        var parent =
            Team.Create(
                "Rai");

        var repository =
            new FakeTeamRepository
            {
                ExistingTeam = parent
            };

        var membershipRepository =
            new FakeTeamMembershipRepository
            {
                Membership =
                    TeamMember.Create(
                        parent.Id,
                        currentUserId,
                        TeamRole.Member)
            };

        var useCase =
            CreateUseCase(
                repository,
                membershipRepository,
                currentUserId);

        var result =
            await useCase.ExecuteAsync(
                "DeskSharing",
                parent.Id,
                CancellationToken.None);

        Assert.Equal(
            CreateTeamStatus.Forbidden,
            result.Status);

        Assert.Null(
            repository.AddedTeam);

        Assert.Null(
            repository.AddedMembership);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHideParentFromNonMember()
    {
        var currentUserId =
            Guid.NewGuid();

        var parent =
            Team.Create(
                "Rai");

        var repository =
            new FakeTeamRepository
            {
                ExistingTeam = parent
            };

        var useCase =
            CreateUseCase(
                repository,
                new FakeTeamMembershipRepository(),
                currentUserId);

        var result =
            await useCase.ExecuteAsync(
                "DeskSharing",
                parent.Id,
                CancellationToken.None);

        Assert.Equal(
            CreateTeamStatus.ParentNotFoundOrNotMember,
            result.Status);

        Assert.Null(
            repository.AddedTeam);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectMissingParent()
    {
        var repository =
            new FakeTeamRepository();

        var useCase =
            CreateUseCase(
                repository,
                new FakeTeamMembershipRepository(),
                Guid.NewGuid());

        var result =
            await useCase.ExecuteAsync(
                "DeskSharing",
                Guid.NewGuid(),
                CancellationToken.None);

        Assert.Equal(
            CreateTeamStatus.ParentNotFoundOrNotMember,
            result.Status);

        Assert.Null(
            repository.AddedTeam);
    }

    private static CreateTeamUseCase CreateUseCase(
        FakeTeamRepository repository,
        FakeTeamMembershipRepository membershipRepository,
        Guid currentUserId)
    {
        return new CreateTeamUseCase(
            repository,
            membershipRepository,
            new FakeCurrentUser(
                currentUserId));
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

    private sealed class FakeTeamRepository
        : ITeamRepository
    {
        public Team? ExistingTeam
        {
            get;
            init;
        }

        public Team? AddedTeam
        {
            get;
            private set;
        }

        public TeamMember? AddedMembership
        {
            get;
            private set;
        }

        public Task<Team?> GetByIdAsync(
            Guid teamId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                ExistingTeam?.Id == teamId
                    ? ExistingTeam
                    : null);
        }

        public Task AddAsync(
            Team team,
            TeamMember ownerMembership,
            CancellationToken cancellationToken)
        {
            AddedTeam =
                team;

            AddedMembership =
                ownerMembership;

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GetTeamsResult>> GetForUserAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<GetTeamsResult> result =
                Array.Empty<GetTeamsResult>();

            return Task.FromResult(
                result);
        }
    }

    private sealed class FakeTeamMembershipRepository
        : ITeamMembershipRepository
    {
        public TeamMember? Membership
        {
            get;
            init;
        }

        public Task<TeamMember?> GetMembershipAsync(
            Guid teamId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var membership =
                Membership is not null &&
                Membership.TeamId == teamId &&
                Membership.UserId == userId
                    ? Membership
                    : null;

            return Task.FromResult(
                membership);
        }

        public Task<bool> IsMemberAsync(
            Guid teamId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Membership is not null &&
                Membership.TeamId == teamId &&
                Membership.UserId == userId);
        }

        public Task AddAsync(
            TeamMember membership,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
