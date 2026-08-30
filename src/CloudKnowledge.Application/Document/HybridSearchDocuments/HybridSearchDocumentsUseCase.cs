using CloudKnowledge.Application.Documents.SearchDocuments;

namespace CloudKnowledge.Application.Documents.HybridSearchDocuments;

public sealed class HybridSearchDocumentsUseCase
{
    private const double ReciprocalRankConstant =
        60.0;

    private readonly SearchDocumentsUseCase
        _semanticSearch;

    private readonly LexicalSearchDocumentsUseCase
        _lexicalSearch;

    private readonly ChunkNavigationQualityClassifier
        _navigationClassifier;

    public HybridSearchDocumentsUseCase(
        SearchDocumentsUseCase semanticSearch,
        LexicalSearchDocumentsUseCase lexicalSearch,
        ChunkNavigationQualityClassifier navigationClassifier)
    {
        _semanticSearch =
            semanticSearch;

        _lexicalSearch =
            lexicalSearch;

        _navigationClassifier =
            navigationClassifier;
    }

    public async Task<HybridSearchDocumentsResult> ExecuteAsync(
        string query,
        int take,
        DocumentRetrievalScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            scope);

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException(
                "Search query cannot be empty.",
                nameof(query));
        }

        if (take < 1 || take > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                "Take must be between 1 and 20.");
        }

        var semanticResults =
            await _semanticSearch.ExecuteAsync(
                query,
                take,
                scope,
                cancellationToken);

        IReadOnlyList<LexicalSearchResult> lexicalResults;

        try
        {
            lexicalResults =
                await _lexicalSearch.ExecuteAsync(
                    query,
                    take,
                    scope,
                    cancellationToken);
        }
        catch (LexicalQuerySyntaxException)
        {
            lexicalResults =
                Array.Empty<LexicalSearchResult>();
        }

        var candidates =
            new Dictionary<Guid, CandidateAggregate>();

        for (var index = 0;
             index < semanticResults.Count;
             index++)
        {
            var result =
                semanticResults[index];

            var candidate =
                GetOrCreate(
                    candidates,
                    result.DocumentId,
                    result.ChunkId,
                    result.Position,
                    result.Content);

            candidate.SemanticRank =
                index + 1;

            candidate.CosineDistance =
                result.CosineDistance;

            candidate.FusedScore +=
                ReciprocalRankContribution(
                    index + 1);
        }

        for (var index = 0;
             index < lexicalResults.Count;
             index++)
        {
            var result =
                lexicalResults[index];

            var candidate =
                GetOrCreate(
                    candidates,
                    result.DocumentId,
                    result.ChunkId,
                    result.Position,
                    result.Content);

            candidate.LexicalRank =
                index + 1;

            candidate.FusedScore +=
                ReciprocalRankContribution(
                    index + 1);
        }

        var orderedResults =
            candidates.Values
                .Select(
                    candidate =>
                    {
                        var navigationPenalty =
                            _navigationClassifier
                                .IsNavigationLike(
                                    candidate.Content);

                        var adjustedScore =
                            _navigationClassifier
                                .ApplyPenalty(
                                    candidate.FusedScore,
                                    navigationPenalty);

                        return new HybridSearchResult(
                            candidate.DocumentId,
                            candidate.ChunkId,
                            candidate.Position,
                            candidate.Content,
                            candidate.CosineDistance,
                            candidate.FusedScore,
                            adjustedScore,
                            candidate.SemanticRank,
                            candidate.LexicalRank,
                            ResolveChannel(
                                candidate.SemanticRank,
                                candidate.LexicalRank),
                            navigationPenalty);
                    })
                .OrderByDescending(
                    result =>
                        result.AdjustedFusedScore)
                .ThenBy(
                    result =>
                        result.CosineDistance
                        ?? double.MaxValue)
                .ThenBy(
                    result =>
                        result.ChunkId)
                .Take(take)
                .ToArray();

        var diagnostics =
            new HybridSearchDiagnostics(
                semanticResults
                    .Select(
                        (result, index) =>
                            new HybridSearchChannelCandidate(
                                result.DocumentId,
                                result.ChunkId,
                                index + 1))
                    .ToArray(),
                lexicalResults
                    .Select(
                        (result, index) =>
                            new HybridSearchChannelCandidate(
                                result.DocumentId,
                                result.ChunkId,
                                index + 1))
                    .ToArray(),
                orderedResults
                    .Select(
                        result =>
                            new HybridSearchFusedCandidate(
                                result.DocumentId,
                                result.ChunkId,
                                result.SemanticRank,
                                result.LexicalRank,
                                result.FusedScore,
                                result.AdjustedFusedScore,
                                result.Channel,
                                result.NavigationPenalty))
                    .ToArray());

        return new HybridSearchDocumentsResult(
            orderedResults,
            diagnostics);
    }

    private static CandidateAggregate GetOrCreate(
        IDictionary<Guid, CandidateAggregate> candidates,
        Guid documentId,
        Guid chunkId,
        int position,
        string content)
    {
        if (candidates.TryGetValue(
                chunkId,
                out var candidate))
        {
            return candidate;
        }

        candidate =
            new CandidateAggregate(
                documentId,
                chunkId,
                position,
                content);

        candidates.Add(
            chunkId,
            candidate);

        return candidate;
    }

    private static double ReciprocalRankContribution(
        int rank)
    {
        return 1.0 /
            (ReciprocalRankConstant + rank);
    }

    private static HybridRetrievalChannel ResolveChannel(
        int? semanticRank,
        int? lexicalRank)
    {
        if (semanticRank.HasValue
            && lexicalRank.HasValue)
        {
            return HybridRetrievalChannel.Both;
        }

        return semanticRank.HasValue
            ? HybridRetrievalChannel.Semantic
            : HybridRetrievalChannel.Lexical;
    }

    private sealed class CandidateAggregate
    {
        public Guid DocumentId { get; }
        public Guid ChunkId { get; }
        public int Position { get; }
        public string Content { get; }
        public double? CosineDistance { get; set; }
        public int? SemanticRank { get; set; }
        public int? LexicalRank { get; set; }
        public double FusedScore { get; set; }

        public CandidateAggregate(
            Guid documentId,
            Guid chunkId,
            int position,
            string content)
        {
            DocumentId =
                documentId;

            ChunkId =
                chunkId;

            Position =
                position;

            Content =
                content;
        }
    }
}
