using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Users;

public sealed class EfUserAccountRepository
    : IUserAccountRepository
{
    private readonly CloudKnowledgeDbContext _dbContext;

    public EfUserAccountRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserAccount?> GetByExternalIdentityAsync(
        string issuer,
        string subject,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new ArgumentException(
                "Issuer cannot be empty.",
                nameof(issuer));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException(
                "Subject cannot be empty.",
                nameof(subject));
        }

        return await _dbContext.UserAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user =>
                    user.ExternalIssuer == issuer &&
                    user.ExternalSubject == subject,
                cancellationToken);
    }

    public async Task AddAsync(
        UserAccount user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        await _dbContext.UserAccounts.AddAsync(
            user,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}