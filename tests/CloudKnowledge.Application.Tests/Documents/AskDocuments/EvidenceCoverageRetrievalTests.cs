using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Tests.Documents.AskDocuments;

public sealed class EvidenceCoverageRetrievalTests
{
    [Fact]
    public async Task ExecuteAsync_CompoundQuestion_ShouldPreserveBestEvidenceFromEachFocusedQuery()
    {
        const string question =
            "Posso installare un ACS880-01 a 3500 metri di altitudine mantenendo la corrente nominale completa? " +
            "Spiega eventuali limitazioni usando esclusivamente la documentazione disponibile.";

        var genericChunks =
            Enumerable.Range(1, 5)
                .Select(index =>
                    new SemanticSearchResult(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        index,
                        $"Generic ACS880-01 installation guidance {index}.",
                        0.10 + (index * 0.01)))
                .ToArray();

        var altitudeChunk =
            new SemanticSearchResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                40,
                "Installation altitude 0...4000 m above sea level.",
                0.18);

        var deratingChunk =
            new SemanticSearchResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                41,
                "Altitude from 1000 to 4000 m: output current is derated by 1% for every 100 m above 1000 m.",
                0.20);

        var queryGenerator =
            new FakeRetrievalQueryGenerator(
                [
                    "ACS880-01 installation altitude 3500 m",
                    "ACS880-01 altitude operating limits",
                    "ACS880-01 output current derating above 1000 m"
                ]);

        var embeddingGenerator =
            new QueryAwareEmbeddingGenerator(
                question,
                queryGenerator.Queries);

        var semanticSearchRepository =
            new QueryAwareSemanticSearchRepository(
                genericChunks,
                altitudeChunk,
                deratingChunk);

        var searchDocumentsUseCase =
            new SearchDocumentsUseCase(
                embeddingGenerator,
                semanticSearchRepository,
                new FakeCurrentUser());

        var answerGenerator =
            new RecordingAnswerGenerator();

        var sut =
            new AskDocumentsUseCase(
                searchDocumentsUseCase,
                answerGenerator,
                queryGenerator);

        var result =
            await sut.ExecuteAsync(
                question,
                5,
                CancellationToken.None);

        Assert.Contains(
            result.Sources,
            source =>
                source.ChunkId == altitudeChunk.ChunkId);

        Assert.Contains(
            result.Sources,
            source =>
                source.ChunkId == deratingChunk.ChunkId);

        Assert.NotNull(answerGenerator.ReceivedSources);

        Assert.Contains(
            answerGenerator.ReceivedSources!,
            source =>
                source.Content.Contains(
                    "1% for every 100 m above 1000 m",
                    StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeRetrievalQueryGenerator
        : IRetrievalQueryGenerator
    {
        public FakeRetrievalQueryGenerator(
            IReadOnlyList<string> queries)
        {
            Queries = queries;
        }

        public IReadOnlyList<string> Queries { get; }

        public Task<IReadOnlyList<string>> GenerateAsync(
            string question,
            int maximumQueries,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(
                Queries.Take(maximumQueries).ToArray());
        }
    }

    private sealed class QueryAwareEmbeddingGenerator
        : IEmbeddingGenerator
    {
        private readonly Dictionary<string, float>
            _queryIds;

        public QueryAwareEmbeddingGenerator(
            string originalQuestion,
            IReadOnlyList<string> focusedQueries)
        {
            _queryIds =
                new Dictionary<string, float>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    [originalQuestion] = 1f
                };

            for (var index = 0;
                 index < focusedQueries.Count;
                 index++)
            {
                _queryIds[focusedQueries[index]] =
                    index + 2f;
            }
        }

        public int Dimensions => 3;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<float[]> embeddings =
                inputs
                    .Select(input =>
                        new[]
                        {
                            _queryIds[input],
                            0f,
                            0f
                        })
                    .ToArray();

            return Task.FromResult(embeddings);
        }
    }

    private sealed class QueryAwareSemanticSearchRepository
        : IDocumentSemanticSearchRepository
    {
        private readonly IReadOnlyList<SemanticSearchResult>
            _genericChunks;

        private readonly SemanticSearchResult
            _altitudeChunk;

        private readonly SemanticSearchResult
            _deratingChunk;

        public QueryAwareSemanticSearchRepository(
            IReadOnlyList<SemanticSearchResult> genericChunks,
            SemanticSearchResult altitudeChunk,
            SemanticSearchResult deratingChunk)
        {
            _genericChunks = genericChunks;
            _altitudeChunk = altitudeChunk;
            _deratingChunk = deratingChunk;
        }

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAccessibleAsync(
            Guid userId,
            float[] queryEmbedding,
            int take,
            DocumentRetrievalScope scope,
            CancellationToken cancellationToken)
        {
            var queryId =
                (int)queryEmbedding[0];

            IReadOnlyList<SemanticSearchResult> results =
                queryId switch
                {
                    2 =>
                        [
                            _altitudeChunk,
                            .. _genericChunks
                        ],
                    4 =>
                        [
                            _deratingChunk,
                            .. _genericChunks
                        ],
                    _ =>
                        _genericChunks
                };

            return Task.FromResult(
                (IReadOnlyList<SemanticSearchResult>)
                results.Take(take).ToArray());
        }
    }

    private sealed class FakeCurrentUser
        : ICurrentUser
    {
        public Task<Guid> GetUserIdAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"));
        }
    }

    private sealed class RecordingAnswerGenerator
        : IAnswerGenerator
    {
        public IReadOnlyList<AnswerContextSource>?
            ReceivedSources { get; private set; }

        public Task<string> GenerateAsync(
            string question,
            IReadOnlyList<AnswerContextSource> sources,
            CancellationToken cancellationToken)
        {
            ReceivedSources = sources;

            return Task.FromResult(
                "Grounded answer.");
        }
    }
}
