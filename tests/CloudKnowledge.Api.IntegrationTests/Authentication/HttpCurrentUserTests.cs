using System.Security.Claims;
using CloudKnowledge.Api.Authentication;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Users;
using Microsoft.AspNetCore.Http;

namespace CloudKnowledge.Api.IntegrationTests.Authentication;

public sealed class HttpCurrentUserTests
{
    [Fact]
    public async Task GetUserIdAsync_WhenUserAlreadyExists_ShouldReturnExistingUserId()
    {
        var user =
            UserAccount.Create(
                "alice@example.com",
                "Alice");

        user.LinkExternalIdentity(
            "https://issuer.example.com/",
            "alice-subject");

        var repository =
            new FakeUserAccountRepository();

        await repository.AddAsync(
            user,
            CancellationToken.None);

        var sut =
            CreateCurrentUser(
                repository,
                CreateAuthenticatedContext(
                    issuer:
                        "https://issuer.example.com/",
                    subject:
                        "alice-subject",
                    email:
                        "alice@example.com",
                    displayName:
                        "Alice"));

        var userId =
            await sut.GetUserIdAsync(
                CancellationToken.None);

        Assert.Equal(
            user.Id,
            userId);

        Assert.Single(
            repository.Users);
    }

    [Fact]
    public async Task GetUserIdAsync_WhenUserDoesNotExist_ShouldProvisionUser()
    {
        var repository =
            new FakeUserAccountRepository();

        var sut =
            CreateCurrentUser(
                repository,
                CreateAuthenticatedContext(
                    issuer:
                        "https://issuer.example.com/",
                    subject:
                        "new-user-subject",
                    email:
                        "newuser@example.com",
                    displayName:
                        "New User"));

        var userId =
            await sut.GetUserIdAsync(
                CancellationToken.None);

        var createdUser =
            Assert.Single(
                repository.Users);

        Assert.Equal(
            userId,
            createdUser.Id);

        Assert.Equal(
            "newuser@example.com",
            createdUser.Email);

        Assert.Equal(
            "New User",
            createdUser.DisplayName);

        Assert.Equal(
            "https://issuer.example.com/",
            createdUser.ExternalIssuer);

        Assert.Equal(
            "new-user-subject",
            createdUser.ExternalSubject);
    }

    [Fact]
    public async Task GetUserIdAsync_WhenNameIsMissing_ShouldUseEmailAsDisplayName()
    {
        var repository =
            new FakeUserAccountRepository();

        var sut =
            CreateCurrentUser(
                repository,
                CreateAuthenticatedContext(
                    issuer:
                        "https://issuer.example.com/",
                    subject:
                        "new-user-subject",
                    email:
                        "newuser@example.com",
                    displayName:
                        null));

        await sut.GetUserIdAsync(
            CancellationToken.None);

        var createdUser =
            Assert.Single(
                repository.Users);

        Assert.Equal(
            "newuser@example.com",
            createdUser.DisplayName);
    }

    [Fact]
    public async Task GetUserIdAsync_WhenRequestIsNotAuthenticated_ShouldThrow()
    {
        var accessor =
            new HttpContextAccessor
            {
                HttpContext =
                    new DefaultHttpContext()
            };

        var sut =
            new HttpCurrentUser(
                accessor,
                new FakeUserAccountRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                sut.GetUserIdAsync(
                    CancellationToken.None));
    }

    [Fact]
    public async Task GetUserIdAsync_WhenNewUserHasNoEmail_ShouldThrow()
    {
        var repository =
            new FakeUserAccountRepository();

        var context =
            CreateAuthenticatedContext(
                issuer:
                    "https://issuer.example.com/",
                subject:
                    "new-user-subject",
                email:
                    null,
                displayName:
                    "New User");

        var sut =
            CreateCurrentUser(
                repository,
                context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                sut.GetUserIdAsync(
                    CancellationToken.None));

        Assert.Empty(
            repository.Users);
    }

    private static HttpCurrentUser CreateCurrentUser(
        IUserAccountRepository repository,
        DefaultHttpContext context)
    {
        var accessor =
            new HttpContextAccessor
            {
                HttpContext =
                    context
            };

        return new HttpCurrentUser(
            accessor,
            repository);
    }

    private static DefaultHttpContext CreateAuthenticatedContext(
        string issuer,
        string subject,
        string? email,
        string? displayName)
    {
        var claims =
            new List<Claim>
            {
                new(
                    "iss",
                    issuer),

                new(
                    "sub",
                    subject)
            };

        if (email is not null)
        {
            claims.Add(
                new Claim(
                    "email",
                    email));
        }

        if (displayName is not null)
        {
            claims.Add(
                new Claim(
                    "name",
                    displayName));
        }

        var identity =
            new ClaimsIdentity(
                claims,
                authenticationType:
                    "Test");

        return new DefaultHttpContext
        {
            User =
                new ClaimsPrincipal(
                    identity)
        };
    }

    private sealed class FakeUserAccountRepository
        : IUserAccountRepository
    {
        public List<UserAccount> Users
        {
            get;
        } = new();

        public Task<UserAccount?> GetByExternalIdentityAsync(
            string issuer,
            string subject,
            CancellationToken cancellationToken)
        {
            var user =
                Users.SingleOrDefault(
                    item =>
                        item.ExternalIssuer == issuer
                        && item.ExternalSubject == subject);

            return Task.FromResult(
                user);
        }

        public Task AddAsync(
            UserAccount user,
            CancellationToken cancellationToken)
        {
            Users.Add(
                user);

            return Task.CompletedTask;
        }
    }
}