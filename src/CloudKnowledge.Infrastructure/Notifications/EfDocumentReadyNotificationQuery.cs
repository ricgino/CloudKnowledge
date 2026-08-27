using CloudKnowledge.Application.Notifications.DocumentReady;
using CloudKnowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Notifications;

public sealed class EfDocumentReadyNotificationQuery
    : IDocumentReadyNotificationQuery
{
    private readonly CloudKnowledgeDbContext _dbContext;

    public EfDocumentReadyNotificationQuery(
        CloudKnowledgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DocumentReadyNotificationAudience?> GetAudienceAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document =
            await (
                from item in _dbContext.Documents.AsNoTracking()
                join owner in _dbContext.UserAccounts.AsNoTracking()
                    on item.OwnerUserId equals owner.Id
                where item.Id == documentId
                select new
                {
                    item.FileName,
                    OwnerUserId = owner.Id,
                    owner.DisplayName
                })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (document is null)
        {
            return null;
        }

        var recipients =
            await (
                from access in _dbContext.DocumentTeamAccess.AsNoTracking()
                join member in _dbContext.TeamMembers.AsNoTracking()
                    on access.TeamId equals member.TeamId
                where access.DocumentId == documentId &&
                      member.UserId != document.OwnerUserId
                select member.UserId)
                .Distinct()
                .ToListAsync(
                    cancellationToken);

        return new DocumentReadyNotificationAudience(
            document.FileName,
            document.OwnerUserId,
            document.DisplayName,
            recipients);
    }
}
