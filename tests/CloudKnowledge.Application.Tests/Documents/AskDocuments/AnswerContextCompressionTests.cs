using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Application.Documents.HybridSearchDocuments;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Tests.Documents.AskDocuments;

public sealed class AnswerContextCompressionTests
{
    [Fact]
    public async Task ExecuteAsync_LongNoisyChunk_ShouldSurfaceRelevantCastMappingInAnswerContext()
    {
        var noise =
            string.Join(
                "\n",
                Enumerable.Range(0, 120)
                    .Select(index => $"Interview discussion line {index}: production process, writing room and animation notes."));

        var cast =
            """
            Principal Cast
            CHIEF Bryan Cranston
            ATARI Koyu Rankin
            MAYOR KOBAYASHI Kunichi Nomura
            REX Edward Norton
            BOSS Bill Murray
            DUKE Jeff Goldblum
            KING Bob Balaban
            SPOTS Liev Schreiber
            """;

        var fullContent =
            $"{noise}\n{cast}";

        var answerGenerator =
            new RecordingAnswerGenerator();

        var sut =
            CreateUseCase(
                fullContent,
                answerGenerator,
                [
                    "Isle of Dogs principal cast character actors",
                    "Isle of Dogs Hero Pack dog characters"
                ]);

        var result =
            await sut.ExecuteAsync(
                "Quali attori doppiano i cani protagonisti del film Isola dei cani?",
                5,
                CancellationToken.None);

        var received =
            Assert.Single(
                Assert.IsAssignableFrom<IReadOnlyList<AnswerContextSource>>(
                    answerGenerator.ReceivedSources));

        Assert.Contains(
            "Principal Cast",
            received.Content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "CHIEF Bryan Cranston",
            received.Content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "REX Edward Norton",
            received.Content,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(
            received.Content.Length < fullContent.Length);

        Assert.Equal(
            fullContent,
            Assert.Single(result.Sources).Content);
    }

    [Fact]
    public async Task ExecuteAsync_LongTechnicalChunk_ShouldPreserveNumericDeratingEvidence()
    {
        var noise =
            string.Join(
                "\n",
                Enumerable.Range(0, 120)
                    .Select(index => $"General installation note {index}: wiring, dimensions and mechanical details."));

        const string evidence =
            "Altitude from 1000 to 4000 m: output current is derated by 1% for every 100 m.";

        var fullContent =
            $"{noise}\n{evidence}\nAdditional unrelated appendix text.";

        var answerGenerator =
            new RecordingAnswerGenerator();

        var sut =
            CreateUseCase(
                fullContent,
                answerGenerator,
                [
                    "ACS880-01 altitude limitations",
                    "ACS880-01 rated output current altitude derating"
                ]);

        await sut.ExecuteAsync(
            "Posso usare un ACS880-01 a 3500 metri mantenendo la corrente nominale?",
            5,
            CancellationToken.None);

        var received =
            Assert.Single(
                Assert.IsAssignableFrom<IReadOnlyList<AnswerContextSource>>(
                    answerGenerator.ReceivedSources));

        Assert.Contains(
            evidence,
            received.Content,
            StringComparison.Ordinal);
        Assert.True(
            received.Content.Length < fullContent.Length);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoReliableCompressionSignal_ShouldKeepOriginalChunk()
    {
        var fullContent =
            string.Join(
                "\n",
                Enumerable.Range(0, 120)
                    .Select(index => $"Completely unrelated archive material {index}: alpha beta gamma delta."));

        var answerGenerator =
            new RecordingAnswerGenerator();

        var sut =
            CreateUseCase(
                fullContent,
                answerGenerator,
                ["zzzxxyy qqqvvv"]);

        await sut.ExecuteAsync(
            "nnnmmm pppkkk",
            5,
            CancellationToken.None);

        var received =
            Assert.Single(
                Assert.IsAssignableFrom<IReadOnlyList<AnswerContextSource>>(
                    answerGenerator.ReceivedSources));

        Assert.Equal(
            fullContent,
            received.Content);
    }

    private static AskDocumentsUseCase CreateUseCase(
        string content,
        RecordingAnswerGenerator answerGenerator,
        IReadOnlyList<string> focusedQueries)
    {
        var searchDocumentsUseCase =
            new SearchDocumentsUseCase(
                new FakeEmbeddingGenerator(),
                new FakeSemanticSearchRepository(
                    new SemanticSearchResult(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        0,
                        content,
                        0.1)),
                new FakeCurrentUser());

        var hybridSearch =
            new HybridSearchDocumentsUseCase(
                searchDocumentsUseCase,
                new ChunkNavigationQualityClassifier());

        return new AskDocumentsUseCase(
            hybridSearch,
            answerGenerator,
            new FakeRetrievalQueryGenerator(
                focusedQueries));
    }

    private sealed class FakeEmbeddingGenerator
        : IEmbeddingGenerator
    {
        public int Dimensions => 3;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<float[]> result =
                inputs.Select(
                        _ => new[] { 0.1f, 0.2f, 0.3f })
                    .ToArray();

            return Task.FromResult(result);
        }
    }

    private sealed class FakeSemanticSearchRepository(
        SemanticSearchResult result)
        : IDocumentSemanticSearchRepository
    {
        public Task<IReadOnlyList<SemanticSearchResult>> SearchAccessibleAsync(
            Guid userId,
            float[] queryEmbedding,
            int take,
            DocumentRetrievalScope scope,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<SemanticSearchResult> results =
                [result];

            return Task.FromResult(results);
        }
    }

    private sealed class FakeCurrentUser
        : ICurrentUser
    {
        public Task<Guid> GetUserIdAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Guid.Parse("11111111-1111-1111-1111-111111111111"));
        }
    }

    private sealed class FakeRetrievalQueryGenerator(
        IReadOnlyList<string> queries)
        : IRetrievalQueryGenerator
    {
        public Task<IReadOnlyList<string>> GenerateAsync(
            string question,
            int maximumQueries,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(
                queries.Take(maximumQueries).ToArray());
        }
    }

    private sealed class RecordingAnswerGenerator
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

            return Task.FromResult("Test answer [S1]");
        }
    }
}
