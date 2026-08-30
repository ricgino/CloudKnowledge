namespace CloudKnowledge.Api.Contracts.Ask;

public sealed record AskDocumentsResponse(
    string Answer,
    IReadOnlyList<AskDocumentSourceResponse> Sources,
    IReadOnlyList<string> RetrievalQueries);