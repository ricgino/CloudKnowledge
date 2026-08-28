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
            await _dbContext.Documents
                .AsNoTracking()
                .Where(item => item.Id == documentId)
                .Select(item => new
                {
                    item.FileName,
                    item.OwnerUserId,
                    item.OwnerTeamId
                })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (document is null)
        {
            return null;
        }

        if (document.OwnerUserId.HasValue)
        {
            var ownerUserId =
                document.OwnerUserId.Value;

            var ownerDisplayName =
                await _dbContext.UserAccounts
                    .AsNoTracking()
                    .Where(owner => owner.Id == ownerUserId)
                    .Select(owner => owner.DisplayName)
                    .SingleOrDefaultAsync(
                        cancellationToken);

            if (ownerDisplayName is null)
            {
                return null;
            }

            var recipients =
                await (
                    from access in _dbContext.DocumentTeamAccess.AsNoTracking()
                    join member in _dbContext.TeamMembers.AsNoTracking()
                        on access.TeamId equals member.TeamId
                    where access.DocumentId == documentId &&
                          member.UserId != ownerUserId
                    select member.UserId)
                    .Distinct()
                    .ToListAsync(
                        cancellationToken);

            return new DocumentReadyNotificationAudience(
                document.FileName,
                ownerUserId,
                ownerDisplayName,
                recipients);
        }

        if (document.OwnerTeamId.HasValue)
        {
            var ownerTeamId =
                document.OwnerTeamId.Value;

            var ownerTeamName =
                await _dbContext.Teams
                    .AsNoTracking()
                    .Where(team => team.Id == ownerTeamId)
                    .Select(team => team.Name)
                    .SingleOrDefaultAsync(
                        cancellationToken);

            if (ownerTeamName is null)
            {
                return null;
            }

            var ownerTeamRecipients =
                _dbContext.TeamMembers
                    .AsNoTracking()
                    .Where(member => member.TeamId == ownerTeamId)
                    .Select(member => member.UserId);

            var explicitlySharedRecipients =
                from access in _dbContext.DocumentTeamAccess.AsNoTracking()
                join member in _dbContext.TeamMembers.AsNoTracking()
                    on access.TeamId equals member.TeamId
                where access.DocumentId == documentId
                select member.UserId;

            var recipients =
                await ownerTeamRecipients
                    .Concat(explicitlySharedRecipients)
                    .Distinct()
                    .ToListAsync(
                        cancellationToken);

            return new DocumentReadyNotificationAudience(
                document.FileName,
                Guid.Empty,
                ownerTeamName,
                recipients);
        }

        return null;
    }
}
