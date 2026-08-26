using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Teams.CreateTeam;
using CloudKnowledge.Application.Teams.GetTeams;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Application.Tests.Teams.CreateTeam;

public sealed class CreateTeamUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCreateTeamWithCurrentUserAsOwner()
    {
        var currentUserId =
            Guid.NewGuid();

        var repository =
            new FakeTeamRepository();

        var useCase =
            new CreateTeamUseCase(
                repository,
                new FakeCurrentUser(
                    currentUserId));

        var result =
            await useCase.ExecuteAsync(
                "  Engineering  ",
                CancellationToken.None);

        Assert.NotNull(
            repository.AddedTeam);

        Assert.NotNull(
            repository.AddedMembership);

        Assert.Equal(
            "Engineering",
            repository.AddedTeam.Name);

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

        Assert.Equal(
            TeamRole.Owner,
            result.Role);
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
}
