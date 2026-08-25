using CloudKnowledge.Application.Users;

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
            principal
                .FindFirst("iss")?
                .Value;

        var subject =
            principal
                .FindFirst("sub")?
                .Value;

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

        var user =
            await _userAccountRepository
                .GetByExternalIdentityAsync(
                    issuer,
                    subject,
                    cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException(
                "The authenticated identity is not linked to a CloudKnowledge user.");
        }

        return user.Id;
    }
}