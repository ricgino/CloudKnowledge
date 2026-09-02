namespace CloudKnowledge.Application.Documents.HybridSearchDocuments;

public sealed record HybridSearchResult(
    Guid DocumentId,
    Guid ChunkId,
    int Position,
    string Content,
    double? CosineDistance,
    double FusedScore,
    double AdjustedFusedScore,
    int? SemanticRank,
    int? LexicalRank,
    HybridRetrievalChannel Channel,
    bool NavigationPenalty);
