using System.Security.Claims;
using CloudKnowledge.Api.Authentication;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Users;
using Microsoft.AspNetCore.Http;

namespace CloudKnowledge.Api.IntegrationTests.Authentication;

public sealed class HttpCurrentUserTests
{
    [Fact]
    public async Task GetUserIdAsync_WhenAuthenticatedIdentityExists_ShouldReturnInternalUserId()
    {
        var user =
            UserAccount.Create(
                "alice@example.com",
                "Alice");

        user.LinkExternalIdentity(
            "https://issuer.example.com/",
            "alice-subject");

        var repository =
            new FakeUserAccountRepository(
                user);

        var httpContext =
            CreateAuthenticatedContext(
                "https://issuer.example.com/",
                "alice-subject");

        var accessor =
            new HttpContextAccessor
            {
                HttpContext =
                    httpContext
            };

        var sut =
            new HttpCurrentUser(
                accessor,
                repository);

        var userId =
            await sut.GetUserIdAsync(
                CancellationToken.None);

        Assert.Equal(
            user.Id,
            userId);
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
    public async Task GetUserIdAsync_WhenExternalIdentityIsUnknown_ShouldThrow()
    {
        var httpContext =
            CreateAuthenticatedContext(
                "https://issuer.example.com/",
                "unknown-subject");

        var accessor =
            new HttpContextAccessor
            {
                HttpContext =
                    httpContext
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

    private static DefaultHttpContext CreateAuthenticatedContext(
        string issuer,
        string subject)
    {
        var identity =
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        "iss",
                        issuer),

                    new Claim(
                        "sub",
                        subject)
                },
                authenticationType:
                    "Test");

        var principal =
            new ClaimsPrincipal(
                identity);

        return new DefaultHttpContext
        {
            User =
                principal
        };
    }

    private sealed class FakeUserAccountRepository
        : IUserAccountRepository
    {
        private readonly UserAccount?
            _user;

        public FakeUserAccountRepository(
            UserAccount? user = null)
        {
            _user =
                user;
        }

        public Task<UserAccount?> GetByExternalIdentityAsync(
            string issuer,
            string subject,
            CancellationToken cancellationToken)
        {
            if (_user is null ||
                _user.ExternalIssuer != issuer ||
                _user.ExternalSubject != subject)
            {
                return Task.FromResult<UserAccount?>(
                    null);
            }

            return Task.FromResult<UserAccount?>(
                _user);
        }

        public Task AddAsync(
            UserAccount user,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}