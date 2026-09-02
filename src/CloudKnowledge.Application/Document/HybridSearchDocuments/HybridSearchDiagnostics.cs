namespace CloudKnowledge.Application.Documents.HybridSearchDocuments;

public sealed record HybridSearchChannelCandidate(
    Guid DocumentId,
    Guid ChunkId,
    int Rank);

public sealed record HybridSearchFusedCandidate(
    Guid DocumentId,
    Guid ChunkId,
    int? SemanticRank,
    int? LexicalRank,
    double FusedScore,
    double AdjustedFusedScore,
    HybridRetrievalChannel Channel,
    bool NavigationPenalty);

public sealed record HybridSearchDiagnostics(
    IReadOnlyList<HybridSearchChannelCandidate> SemanticCandidates,
    IReadOnlyList<HybridSearchChannelCandidate> LexicalCandidates,
    IReadOnlyList<HybridSearchFusedCandidate> HybridCandidates);

public sealed record HybridSearchDocumentsResult(
    IReadOnlyList<HybridSearchResult> Results,
    HybridSearchDiagnostics Diagnostics);
