using CloudKnowledge.Application.Notifications;
using CloudKnowledge.Domain.Notifications;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CloudKnowledge.Infrastructure.Notifications;

public sealed class EfNotificationRepository
    : INotificationRepository
{
    private readonly CloudKnowledgeDbContext _dbContext;

    public EfNotificationRepository(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Notification>> GetRecentAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Notifications
            .AsNoTracking()
            .Where(notification =>
                notification.UserId == userId)
            .OrderByDescending(notification =>
                notification.CreatedAtUtc)
            .ThenByDescending(notification =>
                notification.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Notification?> GetByIdAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Notifications
            .SingleOrDefaultAsync(
                notification =>
                    notification.Id == notificationId &&
                    notification.UserId == userId,
                cancellationToken);
    }

    public async Task<bool> AddIfMissingAsync(
        Notification notification,
        CancellationToken cancellationToken)
    {
        _dbContext.Notifications.Add(
            notification);

        try
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);

            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: "23505"
            })
        {
            _dbContext.Entry(notification).State =
                EntityState.Detached;

            return false;
        }
    }

    public async Task UpdateAsync(
        Notification notification,
        CancellationToken cancellationToken)
    {
        _dbContext.Notifications.Update(
            notification);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
