using CloudKnowledge.Domain.Users;

namespace CloudKnowledge.Application.Users;

public interface IUserDirectoryRepository
{
    Task<UserAccount?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken);
}