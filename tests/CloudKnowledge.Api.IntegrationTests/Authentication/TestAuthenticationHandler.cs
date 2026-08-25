using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Api.IntegrationTests.Authentication;

public sealed class TestAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName =
        "IntegrationTest";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(
            options,
            logger,
            encoder)
    {
    }

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        var claims =
            new[]
            {
                new Claim(
                    "iss",
                    "https://issuer.integration.test/"),

                new Claim(
                    "sub",
                    "integration-user"),

                new Claim(
                    "email",
                    "integration@example.com"),

                new Claim(
                    "name",
                    "Integration User"),

                new Claim(
                    "scp",
                    "access_as_user")
            };

        var identity =
            new ClaimsIdentity(
                claims,
                SchemeName);

        var principal =
            new ClaimsPrincipal(
                identity);

        var ticket =
            new AuthenticationTicket(
                principal,
                SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(
                ticket));
    }
}