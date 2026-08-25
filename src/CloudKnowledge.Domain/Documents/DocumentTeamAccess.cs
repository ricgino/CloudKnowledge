namespace CloudKnowledge.Domain.Documents;

public sealed class DocumentTeamAccess
{
    public Guid DocumentId { get; private set; }

    public Guid TeamId { get; private set; }

    public DateTime SharedAtUtc { get; private set; }

    private DocumentTeamAccess(
        Guid documentId,
        Guid teamId,
        DateTime sharedAtUtc)
    {
        DocumentId = documentId;
        TeamId = teamId;
        SharedAtUtc = sharedAtUtc;
    }

    public static DocumentTeamAccess Create(
        Guid documentId,
        Guid teamId)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document id cannot be empty.",
                nameof(documentId));
        }

        if (teamId == Guid.Empty)
        {
            throw new ArgumentException(
                "Team id cannot be empty.",
                nameof(teamId));
        }

        return new DocumentTeamAccess(
            documentId,
            teamId,
            DateTime.UtcNow);
    }
}