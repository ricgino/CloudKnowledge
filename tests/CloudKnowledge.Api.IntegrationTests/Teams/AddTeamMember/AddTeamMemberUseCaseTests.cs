using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Teams.AddTeamMember;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;

namespace CloudKnowledge.Application.Tests.Teams.AddTeamMember;

public sealed class AddTeamMemberUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCallerIsOwner_ShouldAddUserAsMember()
    {
        var teamId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var targetUser =
            UserAccount.Create(
                "bob@example.com",
                "Bob");

        var membershipRepository =
            new FakeTeamMembershipRepository(
                TeamMember.Create(
                    teamId,
                    ownerId,
                    TeamRole.Owner));

        var useCase =
            new AddTeamMemberUseCase(
                membershipRepository,
                new FakeUserDirectoryRepository(
                    targetUser),
                new FakeCurrentUser(
                    ownerId));

        var result =
            await useCase.ExecuteAsync(
                teamId,
                "bob@example.com",
                CancellationToken.None);

        Assert.Equal(
            AddTeamMemberStatus.Added,
            result.Status);

        Assert.NotNull(
            membershipRepository.AddedMembership);

        Assert.Equal(
            targetUser.Id,
            membershipRepository
                .AddedMembership
                .UserId);

        Assert.Equal(
            TeamRole.Member,
            membershipRepository
                .AddedMembership
                .Role);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerIsOnlyMember_ShouldReturnForbidden()
    {
        var teamId =
            Guid.NewGuid();

        var callerId =
            Guid.NewGuid();

        var membershipRepository =
            new FakeTeamMembershipRepository(
                TeamMember.Create(
                    teamId,
                    callerId,
                    TeamRole.Member));

        var useCase =
            new AddTeamMemberUseCase(
                membershipRepository,
                new FakeUserDirectoryRepository(),
                new FakeCurrentUser(
                    callerId));

        var result =
            await useCase.ExecuteAsync(
                teamId,
                "bob@example.com",
                CancellationToken.None);

        Assert.Equal(
            AddTeamMemberStatus.Forbidden,
            result.Status);

        Assert.Null(
            membershipRepository.AddedMembership);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ShouldReturnUserNotFound()
    {
        var teamId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var membershipRepository =
            new FakeTeamMembershipRepository(
                TeamMember.Create(
                    teamId,
                    ownerId,
                    TeamRole.Owner));

        var useCase =
            new AddTeamMemberUseCase(
                membershipRepository,
                new FakeUserDirectoryRepository(),
                new FakeCurrentUser(
                    ownerId));

        var result =
            await useCase.ExecuteAsync(
                teamId,
                "missing@example.com",
                CancellationToken.None);

        Assert.Equal(
            AddTeamMemberStatus.UserNotFound,
            result.Status);

        Assert.Null(
            membershipRepository.AddedMembership);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsAlreadyMember_ShouldReturnAlreadyMember()
    {
        var teamId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var targetUser =
            UserAccount.Create(
                "bob@example.com",
                "Bob");

        var membershipRepository =
            new FakeTeamMembershipRepository(
                TeamMember.Create(
                    teamId,
                    ownerId,
                    TeamRole.Owner),
                targetUser.Id);

        var useCase =
            new AddTeamMemberUseCase(
                membershipRepository,
                new FakeUserDirectoryRepository(
                    targetUser),
                new FakeCurrentUser(
                    ownerId));

        var result =
            await useCase.ExecuteAsync(
                teamId,
                "bob@example.com",
                CancellationToken.None);

        Assert.Equal(
            AddTeamMemberStatus.AlreadyMember,
            result.Status);

        Assert.Null(
            membershipRepository.AddedMembership);
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

    private sealed class FakeUserDirectoryRepository
        : IUserDirectoryRepository
    {
        private readonly UserAccount?
            _user;

        public FakeUserDirectoryRepository(
            UserAccount? user = null)
        {
            _user =
                user;
        }

        public Task<UserAccount?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken)
        {
            if (_user is null ||
                !string.Equals(
                    _user.Email,
                    email,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<UserAccount?>(
                    null);
            }

            return Task.FromResult<UserAccount?>(
                _user);
        }
    }

    private sealed class FakeTeamMembershipRepository
        : ITeamMembershipRepository
    {
        private readonly TeamMember
            _callerMembership;

        private readonly Guid?
            _existingTargetUserId;

        public FakeTeamMembershipRepository(
            TeamMember callerMembership,
            Guid? existingTargetUserId = null)
        {
            _callerMembership =
                callerMembership;

            _existingTargetUserId =
                existingTargetUserId;
        }

        public TeamMember? AddedMembership
        {
            get;
            private set;
        }

        public Task<TeamMember?> GetMembershipAsync(
            Guid teamId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            if (_callerMembership.TeamId == teamId &&
                _callerMembership.UserId == userId)
            {
                return Task.FromResult<TeamMember?>(
                    _callerMembership);
            }

            return Task.FromResult<TeamMember?>(
                null);
        }

        public Task<bool> IsMemberAsync(
            Guid teamId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _existingTargetUserId == userId);
        }

        public Task AddAsync(
            TeamMember membership,
            CancellationToken cancellationToken)
        {
            AddedMembership =
                membership;

            return Task.CompletedTask;
        }
    }
}