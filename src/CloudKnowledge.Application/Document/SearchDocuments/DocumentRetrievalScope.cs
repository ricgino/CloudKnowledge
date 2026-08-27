namespace CloudKnowledge.Application.Documents.SearchDocuments;

public enum DocumentRetrievalScopeKind
{
    All,
    Team
}

public sealed record DocumentRetrievalScope
{
    public static DocumentRetrievalScope All { get; } =
        new(
            DocumentRetrievalScopeKind.All,
            null,
            false);

    public DocumentRetrievalScopeKind Kind { get; }

    public Guid? TeamId { get; }

    public bool IncludeDescendants { get; }

    private DocumentRetrievalScope(
        DocumentRetrievalScopeKind kind,
        Guid? teamId,
        bool includeDescendants)
    {
        Kind =
            kind;

        TeamId =
            teamId;

        IncludeDescendants =
            includeDescendants;
    }

    public static DocumentRetrievalScope ForTeam(
        Guid teamId,
        bool includeDescendants)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException(
                "Team id cannot be empty.",
                nameof(teamId));
        }

        return new DocumentRetrievalScope(
            DocumentRetrievalScopeKind.Team,
            teamId,
            includeDescendants);
    }
}
