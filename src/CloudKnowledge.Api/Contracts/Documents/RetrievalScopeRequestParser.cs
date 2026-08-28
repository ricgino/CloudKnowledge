using CloudKnowledge.Application.Documents.SearchDocuments;

namespace CloudKnowledge.Api.Contracts.Documents;

public static class RetrievalScopeRequestParser
{
    public static bool TryParse(
        string? rawScope,
        Guid? teamId,
        bool includeDescendants,
        out DocumentRetrievalScope scope,
        out string? errorMessage)
    {
        var normalizedScope =
            string.IsNullOrWhiteSpace(rawScope)
                ? "all"
                : rawScope.Trim().ToLowerInvariant();

        switch (normalizedScope)
        {
            case "all":
                if (teamId.HasValue)
                {
                    scope =
                        DocumentRetrievalScope.All;

                    errorMessage =
                        "teamId is valid only when scope=team.";

                    return false;
                }

                if (includeDescendants)
                {
                    scope =
                        DocumentRetrievalScope.All;

                    errorMessage =
                        "includeDescendants is valid only when scope=team.";

                    return false;
                }

                scope =
                    DocumentRetrievalScope.All;

                errorMessage =
                    null;

                return true;

            case "team":
                if (!teamId.HasValue ||
                    teamId.Value == Guid.Empty)
                {
                    scope =
                        DocumentRetrievalScope.All;

                    errorMessage =
                        "teamId is required and must be a non-empty GUID when scope=team.";

                    return false;
                }

                scope =
                    DocumentRetrievalScope.ForTeam(
                        teamId.Value,
                        includeDescendants);

                errorMessage =
                    null;

                return true;

            default:
                scope =
                    DocumentRetrievalScope.All;

                errorMessage =
                    "Scope must be one of: all, team.";

                return false;
        }
    }
}
