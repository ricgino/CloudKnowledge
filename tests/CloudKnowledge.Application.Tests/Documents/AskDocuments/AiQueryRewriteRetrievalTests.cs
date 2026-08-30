using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Tests.Documents.AskDocuments;

public sealed class AiQueryRewriteRetrievalTests
{
    [Fact]
    public async Task ExecuteAsync_RewrittenTechnicalQuery_ShouldRecoverRelevantChunk()
    {
        const string question =
            "Posso installare un ACS880-01 a 3500 metri di altitudine mantenendo la corrente nominale completa? " +
            "Spiega eventuali limitazioni usando esclusivamente la documentazione disponibile.";

        var relevantResult =
            new SemanticSearchResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                12,
                "Altitude from 1000 to 4000 m: output current is derated by 1% for every 100 m.",
                0.15);

        var embeddingGenerator =
            new QueryAwareEmbeddingGenerator();

        var semanticSearchRepository =
            new QueryAwareSemanticSearchRepository(
                relevantResult);

        var searchDocumentsUseCase =
            new SearchDocumentsUseCase(
                embeddingGenerator,
                semanticSearchRepository,
                new FakeCurrentUser());

        var answerGenerator =
            new FakeAnswerGenerator();

        var retrievalQueryGenerator =
            new FakeRetrievalQueryGenerator(
                "ACS880-01 altitude derating output current");

        var sut =
            new AskDocumentsUseCase(
                searchDocumentsUseCase,
                answerGenerator,
                retrievalQueryGenerator);

        var result =
            await sut.ExecuteAsync(
                question,
                5,
                CancellationToken.None);

        Assert.Single(result.Sources);
        Assert.Equal(
            relevantResult.ChunkId,
            result.Sources[0].ChunkId);

        Assert.Contains(
            embeddingGenerator.ReceivedInputs,
            input =>
                input.Contains(
                    "derating",
                    StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(
            answerGenerator.ReceivedSources);

        Assert.Contains(
            answerGenerator.ReceivedSources!,
            source =>
                source.ChunkId == relevantResult.ChunkId);
    }

    private sealed class FakeRetrievalQueryGenerator(
        params string[] queries)
        : IRetrievalQueryGenerator
    {
        public Task<IReadOnlyList<string>> GenerateAsync(
            string question,
            int maximumQueries,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<string> result =
                queries
                    .Take(maximumQueries)
                    .ToArray();

            return Task.FromResult(result);
        }
    }

    private sealed class QueryAwareEmbeddingGenerator
        : IEmbeddingGenerator
    {
        public int Dimensions => 1;

        public List<string> ReceivedInputs { get; } = [];

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken)
        {
            ReceivedInputs.AddRange(inputs);

            IReadOnlyList<float[]> result =
                inputs
                    .Select(
                        input =>
                            input.Contains(
                                "derating",
                                StringComparison.OrdinalIgnoreCase)
                                ? new[] { 1f }
                                : new[] { 0f })
                    .ToArray();

            return Task.FromResult(result);
        }
    }

    private sealed class QueryAwareSemanticSearchRepository(
        SemanticSearchResult relevantResult)
        : IDocumentSemanticSearchRepository
    {
        public Task<IReadOnlyList<SemanticSearchResult>> SearchAccessibleAsync(
            Guid userId,
            float[] queryEmbedding,
            int take,
            DocumentRetrievalScope scope,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<SemanticSearchResult> result =
                queryEmbedding.Length == 1 &&
                queryEmbedding[0] == 1f
                    ? new[] { relevantResult }
                    : Array.Empty<SemanticSearchResult>();

            return Task.FromResult(result);
        }
    }

    private sealed class FakeCurrentUser
        : ICurrentUser
    {
        private static readonly Guid UserId =
            Guid.Parse(
                "11111111-1111-1111-1111-111111111111");

        public Task<Guid> GetUserIdAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                UserId);
        }
    }

    private sealed class FakeAnswerGenerator
        : IAnswerGenerator
    {
        public IReadOnlyList<AnswerContextSource>? ReceivedSources
        {
            get;
            private set;
        }

        public Task<string> GenerateAsync(
            string question,
            IReadOnlyList<AnswerContextSource> sources,
            CancellationToken cancellationToken)
        {
            ReceivedSources = sources;

            return Task.FromResult(
                "A 3500 m è necessario applicare un derating [S1].");
        }
    }
}
