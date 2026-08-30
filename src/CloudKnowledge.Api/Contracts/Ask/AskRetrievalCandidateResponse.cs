namespace CloudKnowledge.Api.Contracts.Ask;

public sealed record AskRetrievalCandidateResponse(
    Guid DocumentId,
    Guid ChunkId,
    int? Rank,
    int? SemanticRank,
    int? LexicalRank,
    double? FusedScore,
    double? AdjustedFusedScore,
    string? Channel,
    bool? NavigationPenalty,
    bool? Selected);
