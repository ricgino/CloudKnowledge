using CloudKnowledge.Domain.Users;

namespace CloudKnowledge.Application.Users;

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByExternalIdentityAsync(
        string issuer,
        string subject,
        CancellationToken cancellationToken);

    Task AddAsync(
        UserAccount user,
        CancellationToken cancellationToken);
}