using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Application.Documents.HybridSearchDocuments;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Tests.Documents.AskDocuments;

public sealed class HybridAskRetrievalTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPreserveLexicalTechnicalEvidenceInAnswerContextAndSources()
    {
        const string question =
            "Can the equipment maintain rated output current at high installation altitude?";

        const string focusedQuery =
            "rated output current altitude derating";

        var broadDocumentId =
            Guid.NewGuid();

        var broadChunks =
            new[]
            {
                new SemanticSearchResult(
                    broadDocumentId,
                    Guid.NewGuid(),
                    0,
                    "General rated output current and temperature limits.",
                    0.10),
                new SemanticSearchResult(
                    broadDocumentId,
                    Guid.NewGuid(),
                    1,
                    "Table of contents and general technical data.",
                    0.12)
            };

        var evidence =
            new LexicalSearchResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                8,
                "Above the reference installation altitude, rated output current is derated by one percent for each additional one hundred metres.",
                1.0);

        var currentUser =
            new FakeCurrentUser();

        var hybridSearch =
            new HybridSearchDocumentsUseCase(
                new SearchDocumentsUseCase(
                    new FakeEmbeddingGenerator(),
                    new FixedSemanticRepository(
                        broadChunks),
                    currentUser),
                new LexicalSearchDocumentsUseCase(
                    new QueryAwareLexicalRepository(
                        focusedQuery,
                        evidence),
                    currentUser),
                new ChunkNavigationQualityClassifier());

        var answerGenerator =
            new RecordingAnswerGenerator();

        var sut =
            new AskDocumentsUseCase(
                hybridSearch,
                answerGenerator,
                new FixedRetrievalQueryGenerator(
                    new[] { focusedQuery }));

        var result =
            await sut.ExecuteAsync(
                question,
                3,
                CancellationToken.None);

        Assert.NotNull(
            answerGenerator.ReceivedSources);

        Assert.Contains(
            answerGenerator.ReceivedSources!,
            source =>
                source.ChunkId == evidence.ChunkId);

        var evidenceSource =
            Assert.Single(
                result.Sources,
                source =>
                    source.ChunkId == evidence.ChunkId);

        Assert.Null(
            evidenceSource.Similarity);

        var focusedDiagnostics =
            Assert.Single(
                result.RetrievalDiagnostics,
                diagnostics =>
                    diagnostics.Kind ==
                    AskRetrievalQueryKind.Focused);

        Assert.Contains(
            focusedDiagnostics.LexicalCandidates,
            candidate =>
                candidate.ChunkId == evidence.ChunkId);

        Assert.Contains(
            focusedDiagnostics.HybridCandidates,
            candidate =>
                candidate.ChunkId == evidence.ChunkId
                && candidate.Selected);
    }

    [Fact]
    public async Task ExecuteAsync_NearTie_ShouldPreferLessRepresentedDocument()
    {
        var documentA =
            Guid.NewGuid();

        var documentB =
            Guid.NewGuid();

        var firstA =
            new SemanticSearchResult(
                documentA,
                Guid.NewGuid(),
                0,
                "Primary technical evidence from document A.",
                0.10);

        var secondA =
            new SemanticSearchResult(
                documentA,
                Guid.NewGuid(),
                1,
                "Secondary technical evidence from document A.",
                0.11);

        var firstB =
            new SemanticSearchResult(
                documentB,
                Guid.NewGuid(),
                0,
                "Independent technical evidence from document B.",
                0.12);

        var currentUser =
            new FakeCurrentUser();

        var hybridSearch =
            new HybridSearchDocumentsUseCase(
                new SearchDocumentsUseCase(
                    new FakeEmbeddingGenerator(),
                    new FixedSemanticRepository(
                        new[]
                        {
                            firstA,
                            secondA,
                            firstB
                        }),
                    currentUser),
                new LexicalSearchDocumentsUseCase(
                    new EmptyLexicalRepository(),
                    currentUser),
                new ChunkNavigationQualityClassifier());

        var sut =
            new AskDocumentsUseCase(
                hybridSearch,
                new RecordingAnswerGenerator(),
                new FixedRetrievalQueryGenerator(
                    Array.Empty<string>()));

        var result =
            await sut.ExecuteAsync(
                "Compare the available technical evidence.",
                2,
                CancellationToken.None);

        Assert.Equal(
            firstA.ChunkId,
            result.Sources[0].ChunkId);

        Assert.Equal(
            firstB.ChunkId,
            result.Sources[1].ChunkId);
    }

    private sealed class FakeEmbeddingGenerator
        : IEmbeddingGenerator
    {
        public int Dimensions => 3;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<float[]> embeddings =
                inputs
                    .Select(
                        _ =>
                            new[]
                            {
                                1.0f,
                                0.0f,
                                0.0f
                            })
                    .ToArray();

            return Task.FromResult(
                embeddings);
        }
    }

    private sealed class FixedSemanticRepository
        : IDocumentSemanticSearchRepository
    {
        private readonly IReadOnlyList<SemanticSearchResult>
            _results;

        public FixedSemanticRepository(
            IReadOnlyList<SemanticSearchResult> results)
        {
            _results = results;
        }

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAccessibleAsync(
            Guid userId,
            float[] queryEmbedding,
            int take,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                (IReadOnlyList<SemanticSearchResult>)
                _results.Take(take).ToArray());
        }

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAccessibleAsync(
            Guid userId,
            float[] queryEmbedding,
            int take,
            DocumentRetrievalScope scope,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                (IReadOnlyList<SemanticSearchResult>)
                _results.Take(take).ToArray());
        }
    }

    private sealed class QueryAwareLexicalRepository
        : IDocumentLexicalSearchRepository
    {
        private readonly string
            _focusedQuery;

        private readonly LexicalSearchResult
            _evidence;

        public QueryAwareLexicalRepository(
            string focusedQuery,
            LexicalSearchResult evidence)
        {
            _focusedQuery = focusedQuery;
            _evidence = evidence;
        }

        public Task<IReadOnlyList<LexicalSearchResult>> SearchAccessibleAsync(
            Guid userId,
            string query,
            int take,
            DocumentRetrievalScope scope,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<LexicalSearchResult> results =
                string.Equals(
                    query,
                    _focusedQuery,
                    StringComparison.OrdinalIgnoreCase)
                    ? new[] { _evidence }
                    : Array.Empty<LexicalSearchResult>();

            return Task.FromResult(
                results);
        }
    }

    private sealed class EmptyLexicalRepository
        : IDocumentLexicalSearchRepository
    {
        public Task<IReadOnlyList<LexicalSearchResult>> SearchAccessibleAsync(
            Guid userId,
            string query,
            int take,
            DocumentRetrievalScope scope,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<LexicalSearchResult>>(
                Array.Empty<LexicalSearchResult>());
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

    private sealed class FixedRetrievalQueryGenerator
        : IRetrievalQueryGenerator
    {
        private readonly IReadOnlyList<string>
            _queries;

        public FixedRetrievalQueryGenerator(
            IReadOnlyList<string> queries)
        {
            _queries = queries;
        }

        public Task<IReadOnlyList<string>> GenerateAsync(
            string question,
            int maximumQueries,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(
                _queries.Take(maximumQueries).ToArray());
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
