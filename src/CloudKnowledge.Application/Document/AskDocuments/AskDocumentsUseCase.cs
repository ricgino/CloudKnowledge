using CloudKnowledge.Application.Documents.SearchDocuments;

namespace CloudKnowledge.Application.Documents.AskDocuments;

public sealed class AskDocumentsUseCase
{
    private const int MaximumFocusedQueries =
        3;

    private const int ReciprocalRankConstant =
        60;

    private readonly SearchDocumentsUseCase
        _searchDocumentsUseCase;

    private readonly IAnswerGenerator
        _answerGenerator;

    private readonly IRetrievalQueryGenerator?
        _retrievalQueryGenerator;

    public AskDocumentsUseCase(
        SearchDocumentsUseCase searchDocumentsUseCase,
        IAnswerGenerator answerGenerator)
        : this(
            searchDocumentsUseCase,
            answerGenerator,
            retrievalQueryGenerator: null)
    {
    }

    public AskDocumentsUseCase(
        SearchDocumentsUseCase searchDocumentsUseCase,
        IAnswerGenerator answerGenerator,
        IRetrievalQueryGenerator? retrievalQueryGenerator)
    {
        _searchDocumentsUseCase =
            searchDocumentsUseCase ??
            throw new ArgumentNullException(
                nameof(searchDocumentsUseCase));

        _answerGenerator =
            answerGenerator ??
            throw new ArgumentNullException(
                nameof(answerGenerator));

        _retrievalQueryGenerator =
            retrievalQueryGenerator;
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

        var searchResults =
            await RetrieveAsync(
                question,
                take,
                scope,
                cancellationToken);

        if (searchResults.Count == 0)
        {
            return new AskDocumentsResult(
                "Non sono state trovate informazioni pertinenti nei documenti.",
                Array.Empty<AskDocumentsSource>());
        }

        var contextSources =
            searchResults
                .Select(
                    (result, index) =>
                        new AnswerContextSource(
                            $"S{index + 1}",
                            result.DocumentId,
                            result.ChunkId,
                            result.Position,
                            result.Content))
                .ToArray();

        var answer =
            await _answerGenerator.GenerateAsync(
                question,
                contextSources,
                cancellationToken);

        var sources =
            searchResults
                .Select(
                    (result, index) =>
                        new AskDocumentsSource(
                            $"S{index + 1}",
                            result.DocumentId,
                            result.ChunkId,
                            result.Position,
                            result.Content,
                            1.0 - result.CosineDistance))
                .ToArray();

        return new AskDocumentsResult(
            answer,
            sources);
    }

    private async Task<IReadOnlyList<SemanticSearchResult>> RetrieveAsync(
        string question,
        int take,
        DocumentRetrievalScope scope,
        CancellationToken cancellationToken)
    {
        var focusedQueries =
            _retrievalQueryGenerator is null
                ? RetrievalQueryPlanner.CreateFocusedQueries(
                    question,
                    MaximumFocusedQueries)
                : await _retrievalQueryGenerator.GenerateAsync(
                    question,
                    MaximumFocusedQueries,
                    cancellationToken);

        var queries =
            new[]
            {
                question.Trim()
            }
            .Concat(
                focusedQueries)
            .Where(
                query =>
                    !string.IsNullOrWhiteSpace(query))
            .Select(
                query =>
                    query.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidateTake =
            Math.Min(
                20,
                Math.Max(
                    8,
                    take * 2));

        var fusedResults =
            new Dictionary<Guid, FusedSearchResult>();

        foreach (var query in queries)
        {
            var queryResults =
                await _searchDocumentsUseCase.ExecuteAsync(
                    query,
                    candidateTake,
                    scope,
                    cancellationToken);

            for (var index = 0;
                 index < queryResults.Count;
                 index++)
            {
                var result =
                    queryResults[index];

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
                    new FusedSearchResult(
                        result,
                        reciprocalRankScore));
            }
        }

        return fusedResults
            .Values
            .OrderByDescending(
                result => result.Score)
            .ThenBy(
                result => result.Result.CosineDistance)
            .ThenBy(
                result => result.Result.ChunkId)
            .Take(take)
            .Select(
                result => result.Result)
            .ToArray();
    }

    private sealed class FusedSearchResult
    {
        public FusedSearchResult(
            SemanticSearchResult result,
            double score)
        {
            Result =
                result;

            Score =
                score;
        }

        public SemanticSearchResult Result
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
            SemanticSearchResult result,
            double score)
        {
            Score +=
                score;

            if (result.CosineDistance <
                Result.CosineDistance)
            {
                Result =
                    result;
            }
        }
    }
}
