using System.Security.Claims;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Users;

namespace CloudKnowledge.Api.Authentication;

public sealed class HttpCurrentUser
    : ICurrentUser
{
    private readonly IHttpContextAccessor
        _httpContextAccessor;

    private readonly IUserAccountRepository
        _userAccountRepository;

    public HttpCurrentUser(
        IHttpContextAccessor httpContextAccessor,
        IUserAccountRepository userAccountRepository)
    {
        _httpContextAccessor =
            httpContextAccessor;

        _userAccountRepository =
            userAccountRepository;
    }

    public async Task<Guid> GetUserIdAsync(
        CancellationToken cancellationToken)
    {
        var principal =
            _httpContextAccessor
                .HttpContext?
                .User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException(
                "No authenticated user is available.");
        }

        var issuer =
            principal.FindFirst("iss")?.Value;

        var subject =
            principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException(
                "The authenticated identity does not contain an issuer.");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException(
                "The authenticated identity does not contain a subject.");
        }

        var existingUser =
            await _userAccountRepository
                .GetByExternalIdentityAsync(
                    issuer,
                    subject,
                    cancellationToken);

        if (existingUser is not null)
        {
            return existingUser.Id;
        }

        var email =
            principal.FindFirst("email")?.Value
            ?? principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                "The authenticated identity does not contain an email address.");
        }

        var displayName =
            principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value
            ?? email;

        var newUser =
            UserAccount.Create(
                email,
                displayName);

        newUser.LinkExternalIdentity(
            issuer,
            subject);

        await _userAccountRepository.AddAsync(
            newUser,
            cancellationToken);

        return newUser.Id;
    }
}