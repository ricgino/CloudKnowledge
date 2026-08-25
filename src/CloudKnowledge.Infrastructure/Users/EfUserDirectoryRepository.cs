using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Users;

public sealed class EfUserDirectoryRepository
    : IUserDirectoryRepository
{
    private readonly CloudKnowledgeDbContext
        _dbContext;

    public EfUserDirectoryRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task<UserAccount?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(
                "Email cannot be empty.",
                nameof(email));
        }

        var normalizedEmail =
            email.Trim().ToLowerInvariant();

        return await _dbContext.UserAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user =>
                    user.Email.ToLower() ==
                    normalizedEmail,
                cancellationToken);
    }
}