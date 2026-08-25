using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Infrastructure.Persistence;

namespace CloudKnowledge.Infrastructure.Documents;

internal static class DocumentAccessQueryExtensions
{
    public static IQueryable<Document> WhereAccessibleTo(
        this IQueryable<Document> documents,
        CloudKnowledgeDbContext dbContext,
        Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User id cannot be empty.",
                nameof(userId));
        }

        return documents.Where(
            document =>
                document.OwnerUserId == userId

                ||

                dbContext.DocumentTeamAccess.Any(
                    access =>
                        access.DocumentId == document.Id

                        &&

                        dbContext.TeamMembers.Any(
                            member =>
                                member.TeamId == access.TeamId
                                && member.UserId == userId)));
    }
}