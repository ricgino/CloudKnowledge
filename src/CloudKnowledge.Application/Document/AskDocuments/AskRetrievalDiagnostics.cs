using CloudKnowledge.Application.Documents.HybridSearchDocuments;

namespace CloudKnowledge.Application.Documents.AskDocuments;

public enum AskRetrievalQueryKind
{
    Original,
    Focused
}

public sealed record AskRetrievalHybridCandidate(
    Guid DocumentId,
    Guid ChunkId,
    int? SemanticRank,
    int? LexicalRank,
    double FusedScore,
    double AdjustedFusedScore,
    HybridRetrievalChannel Channel,
    bool NavigationPenalty,
    bool Selected);

public sealed record AskRetrievalQueryDiagnostics(
    AskRetrievalQueryKind Kind,
    string Query,
    IReadOnlyList<HybridSearchChannelCandidate> SemanticCandidates,
    IReadOnlyList<HybridSearchChannelCandidate> LexicalCandidates,
    IReadOnlyList<AskRetrievalHybridCandidate> HybridCandidates);
