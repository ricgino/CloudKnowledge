using CloudKnowledge.Application.Documents.HybridSearchDocuments;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Documents.AskDocuments;

public sealed class AskDocumentsUseCase
{
    private const int MaximumFocusedQueries =
        3;

    private const int ReciprocalRankConstant =
        60;

    private const double NearTieRatio =
        0.98;

    private const int MaximumDiagnosticCandidates =
        8;

    private const int MaximumAdjacentContextChunks =
        3;

    private readonly HybridSearchDocumentsUseCase
        _hybridSearchDocumentsUseCase;

    private readonly IAnswerGenerator
        _answerGenerator;

    private readonly IRetrievalQueryGenerator?
        _retrievalQueryGenerator;

    private readonly ICurrentUser?
        _currentUser;

    private readonly IDocumentChunkContextRepository?
        _documentChunkContextRepository;

    public AskDocumentsUseCase(
        SearchDocumentsUseCase searchDocumentsUseCase,
        IAnswerGenerator answerGenerator)
        : this(
            new HybridSearchDocumentsUseCase(
                searchDocumentsUseCase,
                new ChunkNavigationQualityClassifier()),
            answerGenerator,
            retrievalQueryGenerator: null,
            currentUser: null,
            documentChunkContextRepository: null)
    {
    }

    public AskDocumentsUseCase(
        HybridSearchDocumentsUseCase hybridSearchDocumentsUseCase,
        IAnswerGenerator answerGenerator)
        : this(
            hybridSearchDocumentsUseCase,
            answerGenerator,
            retrievalQueryGenerator: null,
            currentUser: null,
            documentChunkContextRepository: null)
    {
    }

    public AskDocumentsUseCase(
        HybridSearchDocumentsUseCase hybridSearchDocumentsUseCase,
        IAnswerGenerator answerGenerator,
        IRetrievalQueryGenerator? retrievalQueryGenerator)
        : this(
            hybridSearchDocumentsUseCase,
            answerGenerator,
            retrievalQueryGenerator,
            currentUser: null,
            documentChunkContextRepository: null)
    {
    }

    public AskDocumentsUseCase(
        HybridSearchDocumentsUseCase hybridSearchDocumentsUseCase,
        IAnswerGenerator answerGenerator,
        IRetrievalQueryGenerator? retrievalQueryGenerator,
        ICurrentUser? currentUser,
        IDocumentChunkContextRepository? documentChunkContextRepository)
    {
        _hybridSearchDocumentsUseCase =
            hybridSearchDocumentsUseCase ??
            throw new ArgumentNullException(
                nameof(hybridSearchDocumentsUseCase));

        _answerGenerator =
            answerGenerator ??
            throw new ArgumentNullException(
                nameof(answerGenerator));

        _retrievalQueryGenerator =
            retrievalQueryGenerator;

        _currentUser =
            currentUser;

        _documentChunkContextRepository =
            documentChunkContextRepository;
    }

    public Task<AskDocumentsResult> ExecuteAsync(
        string question,
        int take,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            question,
            take,
            DocumentRetrievalScope.All,
            cancellationToken);
    }

    public async Task<AskDocumentsResult> ExecuteAsync(
        string question,
        int take,
        DocumentRetrievalScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            scope);

        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException(
                "Question cannot be empty.",
                nameof(question));
        }

        if (take < 1 || take > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                "Take must be between 1 and 10.");
        }

        var retrieval =
            await RetrieveAsync(
                question,
                take,
                scope,
                cancellationToken);

        if (retrieval.Results.Count == 0)
        {
            return new AskDocumentsResult(
                "Non sono state trovate informazioni pertinenti nei documenti.",
                Array.Empty<AskDocumentsSource>(),
                retrieval.Queries,
                retrieval.Diagnostics);
        }

        var evidence =
            await BuildAnswerEvidenceAsync(
                retrieval.Results,
                scope,
                cancellationToken);

        var contextSources =
            evidence
                .Select(
                    (item, index) =>
                        new AnswerContextSource(
                            $"S{index + 1}",
                            item.DocumentId,
                            item.ChunkId,
                            item.Position,
                            AnswerContextCompressor.Compress(
                                item.Content,
                                retrieval.Queries)))
                .ToArray();

        var answer =
            await _answerGenerator.GenerateAsync(
                question,
                contextSources,
                cancellationToken);

        var sources =
            evidence
                .Select(
                    (item, index) =>
                        new AskDocumentsSource(
                            $"S{index + 1}",
                            item.DocumentId,
                            item.ChunkId,
                            item.Position,
                            item.Content,
                            item.Similarity))
                .ToArray();

        return new AskDocumentsResult(
            answer,
            sources,
            retrieval.Queries,
            retrieval.Diagnostics);
    }

    private async Task<IReadOnlyList<AnswerEvidence>> BuildAnswerEvidenceAsync(
        IReadOnlyList<HybridSearchResult> retrievalResults,
        DocumentRetrievalScope scope,
        CancellationToken cancellationToken)
    {
        var evidence =
            retrievalResults
                .Select(
                    result =>
                        new AnswerEvidence(
                            result.DocumentId,
                            result.ChunkId,
                            result.Position,
                            result.Content,
                            result.CosineDistance.HasValue
                                ? 1.0 - result.CosineDistance.Value
                                : null))
                .ToList();

        if (_currentUser is null ||
            _documentChunkContextRepository is null)
        {
            return evidence;
        }

        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var includedChunkIds =
            evidence
                .Select(
                    item =>
                        item.ChunkId)
                .ToHashSet();

        var added =
            0;

        foreach (var anchor in retrievalResults)
        {
            if (added >= MaximumAdjacentContextChunks)
            {
                break;
            }

            var next =
                await _documentChunkContextRepository.GetAccessibleNextAsync(
                    userId,
                    anchor.DocumentId,
                    anchor.Position,
                    scope,
                    cancellationToken);

            if (next is null ||
                !includedChunkIds.Add(
                    next.ChunkId))
            {
                continue;
            }

            evidence.Add(
                new AnswerEvidence(
                    next.DocumentId,
                    next.ChunkId,
                    next.Position,
                    next.Content,
                    Similarity: null));

            added++;
        }

        return evidence;
    }

    private async Task<RetrievalExecution> RetrieveAsync(
        string question,
        int take,
        DocumentRetrievalScope scope,
        CancellationToken cancellationToken)
    {
        var originalQuery =
            question.Trim();

        var focusedQueries =
            _retrievalQueryGenerator is null
                ? RetrievalQueryPlanner.CreateFocusedQueries(
                    question,
                    MaximumFocusedQueries)
                : await _retrievalQueryGenerator.GenerateAsync(
                    question,
                    MaximumFocusedQueries,
                    cancellationToken);

        var normalizedFocusedQueries =
            focusedQueries
                .Where(
                    query =>
                        !string.IsNullOrWhiteSpace(query))
                .Select(
                    query =>
                        query.Trim())
                .Where(
                    query =>
                        !string.Equals(
                            query,
                            originalQuery,
                            StringComparison.OrdinalIgnoreCase))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(
                    MaximumFocusedQueries)
                .ToArray();

        var candidateTake =
            Math.Min(
                20,
                Math.Max(
                    8,
                    take * 2));

        var executions =
            new List<QueryExecution>();

        var allQueries =
            new[]
            {
                originalQuery
            }
            .Concat(
                normalizedFocusedQueries)
            .ToArray();

        for (var index = 0;
             index < allQueries.Length;
             index++)
        {
            var query =
                allQueries[index];

            var result =
                await _hybridSearchDocumentsUseCase.ExecuteAsync(
                    query,
                    candidateTake,
                    scope,
                    cancellationToken);

            executions.Add(
                new QueryExecution(
                    index == 0
                        ? AskRetrievalQueryKind.Original
                        : AskRetrievalQueryKind.Focused,
                    query,
                    result));
        }

        var fusedResults =
            new Dictionary<Guid, CrossQueryFusedResult>();

        foreach (var execution in executions)
        {
            for (var index = 0;
                 index < execution.Result.Results.Count;
                 index++)
            {
                var result =
                    execution.Result.Results[index];

                var reciprocalRankScore =
                    1d /
                    (ReciprocalRankConstant + index + 1d);

                if (fusedResults.TryGetValue(
                        result.ChunkId,
                        out var fusedResult))
                {
                    fusedResult.Add(
                        result,
                        reciprocalRankScore);

                    continue;
                }

                fusedResults.Add(
                    result.ChunkId,
                    new CrossQueryFusedResult(
                        result,
                        reciprocalRankScore));
            }
        }

        var selectedResults =
            new List<HybridSearchResult>();

        var selectedChunkIds =
            new HashSet<Guid>();

        var selectedPerDocument =
            new Dictionary<Guid, int>();

        foreach (var execution in executions.Where(
                     item =>
                         item.Kind ==
                         AskRetrievalQueryKind.Focused))
        {
            if (selectedResults.Count >= take)
            {
                break;
            }

            var evidence =
                execution.Result.Results
                    .FirstOrDefault(
                        result =>
                            !selectedChunkIds.Contains(
                                result.ChunkId));

            if (evidence is null)
            {
                continue;
            }

            var representative =
                fusedResults[evidence.ChunkId]
                    .Result;

            AddSelected(
                representative,
                selectedResults,
                selectedChunkIds,
                selectedPerDocument);
        }

        var remaining =
            fusedResults.Values
                .Where(
                    result =>
                        !selectedChunkIds.Contains(
                            result.Result.ChunkId))
                .ToList();

        while (selectedResults.Count < take
               && remaining.Count > 0)
        {
            var ranked =
                remaining
                    .OrderByDescending(
                        result =>
                            result.Score)
                    .ThenBy(
                        result =>
                            result.Result.CosineDistance
                            ?? double.MaxValue)
                    .ThenBy(
                        result =>
                            result.Result.ChunkId)
                    .ToArray();

            var bestScore =
                ranked[0].Score;

            var nearTieCandidates =
                ranked
                    .Where(
                        result =>
                            IsNearTie(
                                result.Score,
                                bestScore))
                    .ToArray();

            var chosen =
                nearTieCandidates
                    .OrderBy(
                        result =>
                            selectedPerDocument.TryGetValue(
                                result.Result.DocumentId,
                                out var count)
                                ? count
                                : 0)
                    .ThenByDescending(
                        result =>
                            result.Score)
                    .ThenBy(
                        result =>
                            result.Result.CosineDistance
                            ?? double.MaxValue)
                    .ThenBy(
                        result =>
                            result.Result.ChunkId)
                    .First();

            AddSelected(
                chosen.Result,
                selectedResults,
                selectedChunkIds,
                selectedPerDocument);

            remaining.Remove(
                chosen);
        }

        var diagnostics =
            BuildDiagnostics(
                executions,
                selectedChunkIds);

        return new RetrievalExecution(
            selectedResults,
            allQueries,
            diagnostics);
    }

    private static IReadOnlyList<AskRetrievalQueryDiagnostics> BuildDiagnostics(
        IReadOnlyList<QueryExecution> executions,
        IReadOnlySet<Guid> selectedChunkIds)
    {
        return executions
            .Select(
                execution =>
                    new AskRetrievalQueryDiagnostics(
                        execution.Kind,
                        execution.Query,
                        execution.Result.Diagnostics
                            .SemanticCandidates
                            .Take(
                                MaximumDiagnosticCandidates)
                            .ToArray(),
                        execution.Result.Diagnostics
                            .LexicalCandidates
                            .Take(
                                MaximumDiagnosticCandidates)
                            .ToArray(),
                        execution.Result.Results
                            .Take(
                                MaximumDiagnosticCandidates)
                            .Select(
                                result =>
                                    new AskRetrievalHybridCandidate(
                                        result.DocumentId,
                                        result.ChunkId,
                                        result.SemanticRank,
                                        result.LexicalRank,
                                        result.FusedScore,
                                        result.AdjustedFusedScore,
                                        result.Channel,
                                        result.NavigationPenalty,
                                        selectedChunkIds.Contains(
                                            result.ChunkId)))
                            .ToArray()))
            .ToArray();
    }

    private static bool IsNearTie(
        double candidateScore,
        double bestScore)
    {
        if (bestScore <= 0)
        {
            return candidateScore == bestScore;
        }

        return candidateScore >=
            bestScore * NearTieRatio;
    }

    private static void AddSelected(
        HybridSearchResult result,
        ICollection<HybridSearchResult> selectedResults,
        ISet<Guid> selectedChunkIds,
        IDictionary<Guid, int> selectedPerDocument)
    {
        if (!selectedChunkIds.Add(
                result.ChunkId))
        {
            return;
        }

        selectedResults.Add(
            result);

        selectedPerDocument[result.DocumentId] =
            selectedPerDocument.TryGetValue(
                result.DocumentId,
                out var count)
                ? count + 1
                : 1;
    }

    private sealed record AnswerEvidence(
        Guid DocumentId,
        Guid ChunkId,
        int Position,
        string Content,
        double? Similarity);

    private sealed record QueryExecution(
        AskRetrievalQueryKind Kind,
        string Query,
        HybridSearchDocumentsResult Result);

    private sealed record RetrievalExecution(
        IReadOnlyList<HybridSearchResult> Results,
        IReadOnlyList<string> Queries,
        IReadOnlyList<AskRetrievalQueryDiagnostics> Diagnostics);

    private sealed class CrossQueryFusedResult
    {
        public CrossQueryFusedResult(
            HybridSearchResult result,
            double score)
        {
            Result =
                result;

            Score =
                score;
        }

        public HybridSearchResult Result
        {
            get;
            private set;
        }

        public double Score
        {
            get;
            private set;
        }

        public void Add(
            HybridSearchResult result,
            double score)
        {
            Score +=
                score;

            if (ShouldPreferRepresentative(
                    result,
                    Result))
            {
                Result =
                    result;
            }
        }

        private static bool ShouldPreferRepresentative(
            HybridSearchResult candidate,
            HybridSearchResult current)
        {
            if (candidate.CosineDistance.HasValue
                && !current.CosineDistance.HasValue)
            {
                return true;
            }

            if (candidate.CosineDistance.HasValue
                && current.CosineDistance.HasValue
                && candidate.CosineDistance.Value <
                current.CosineDistance.Value)
            {
                return true;
            }

            return candidate.CosineDistance.HasValue ==
                   current.CosineDistance.HasValue
                   && candidate.AdjustedFusedScore >
                   current.AdjustedFusedScore;
        }
    }
}
