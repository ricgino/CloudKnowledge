namespace CloudKnowledge.Api.Contracts.Ask;

public sealed record AskRetrievalQueryDiagnosticsResponse(
    string Kind,
    string Query,
    IReadOnlyList<AskRetrievalCandidateResponse> SemanticCandidates,
    IReadOnlyList<AskRetrievalCandidateResponse> LexicalCandidates,
    IReadOnlyList<AskRetrievalCandidateResponse> HybridCandidates);
