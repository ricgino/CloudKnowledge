using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Application.Documents.SearchDocuments;

namespace CloudKnowledge.Application.Tests.Documents.AskDocuments;

public sealed class AskDocumentsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSearchReturnsResults_ShouldGenerateAnswerAndSources()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var firstChunkId = Guid.NewGuid();
        var secondChunkId = Guid.NewGuid();

        var searchResults = new[]
        {
            new SemanticSearchResult(
                documentId,
                firstChunkId,
                3,
                "First relevant chunk.",
                0.20),

            new SemanticSearchResult(
                documentId,
                secondChunkId,
                7,
                "Second relevant chunk.",
                0.35)
        };

        var embeddingGenerator =
            new FakeEmbeddingGenerator();

        var semanticSearchRepository =
            new FakeSemanticSearchRepository(
                searchResults);

        var searchDocumentsUseCase =
            new SearchDocumentsUseCase(
                embeddingGenerator,
                semanticSearchRepository);

        var answerGenerator =
            new FakeAnswerGenerator(
                "Generated answer [S1]");

        var sut =
            new AskDocumentsUseCase(
                searchDocumentsUseCase,
                answerGenerator);

        // Act
        var result =
            await sut.ExecuteAsync(
                "What does the document say?",
                5,
                CancellationToken.None);

        // Assert
        Assert.Equal(
            "Generated answer [S1]",
            result.Answer);

        Assert.Equal(
            2,
            result.Sources.Count);

        Assert.Equal(
            "S1",
            result.Sources[0].Label);

        Assert.Equal(
            documentId,
            result.Sources[0].DocumentId);

        Assert.Equal(
            firstChunkId,
            result.Sources[0].ChunkId);

        Assert.Equal(
            3,
            result.Sources[0].Position);

        Assert.Equal(
            "First relevant chunk.",
            result.Sources[0].Content);

        Assert.Equal(
            0.80,
            result.Sources[0].Similarity,
            precision: 10);

        Assert.Equal(
            "S2",
            result.Sources[1].Label);

        Assert.Equal(
            0.65,
            result.Sources[1].Similarity,
            precision: 10);

        Assert.NotNull(
            answerGenerator.ReceivedSources);

        Assert.Equal(
            2,
            answerGenerator.ReceivedSources!.Count);

        Assert.Equal(
            "S1",
            answerGenerator.ReceivedSources[0].Label);

        Assert.Equal(
            "S2",
            answerGenerator.ReceivedSources[1].Label);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoSearchResults_ShouldReturnFallbackWithoutCallingGenerator()
    {
        // Arrange
        var embeddingGenerator =
            new FakeEmbeddingGenerator();

        var semanticSearchRepository =
            new FakeSemanticSearchRepository(
                Array.Empty<SemanticSearchResult>());

        var searchDocumentsUseCase =
            new SearchDocumentsUseCase(
                embeddingGenerator,
                semanticSearchRepository);

        var answerGenerator =
            new FakeAnswerGenerator(
                "This must never be returned.");

        var sut =
            new AskDocumentsUseCase(
                searchDocumentsUseCase,
                answerGenerator);

        // Act
        var result =
            await sut.ExecuteAsync(
                "Question with no results",
                5,
                CancellationToken.None);

        // Assert
        Assert.Equal(
            "Non sono state trovate informazioni pertinenti nei documenti.",
            result.Answer);

        Assert.Empty(
            result.Sources);

        Assert.False(
            answerGenerator.WasCalled);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("     ")]
    public async Task ExecuteAsync_WhenQuestionIsEmpty_ShouldThrowArgumentException(
        string question)
    {
        // Arrange
        var sut =
            CreateUseCase();

        // Act
        var action =
            async () =>
                await sut.ExecuteAsync(
                    question,
                    5,
                    CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(
            action);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(100)]
    public async Task ExecuteAsync_WhenTakeIsOutsideAllowedRange_ShouldThrow(
        int take)
    {
        // Arrange
        var sut =
            CreateUseCase();

        // Act
        var action =
            async () =>
                await sut.ExecuteAsync(
                    "Valid question",
                    take,
                    CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            action);
    }

    private static AskDocumentsUseCase CreateUseCase()
    {
        var searchDocumentsUseCase =
            new SearchDocumentsUseCase(
                new FakeEmbeddingGenerator(),
                new FakeSemanticSearchRepository(
                    Array.Empty<SemanticSearchResult>()));

        return new AskDocumentsUseCase(
            searchDocumentsUseCase,
            new FakeAnswerGenerator(
                "Generated answer"));
    }

    private sealed class FakeEmbeddingGenerator
        : IEmbeddingGenerator
    {
        public int Dimensions =>
            3;

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
                                0.1f,
                                0.2f,
                                0.3f
                            })
                    .ToArray();

            return Task.FromResult(
                embeddings);
        }
    }

    private sealed class FakeSemanticSearchRepository
        : IDocumentSemanticSearchRepository
    {
        private readonly IReadOnlyList<SemanticSearchResult>
            _results;

        public FakeSemanticSearchRepository(
            IReadOnlyList<SemanticSearchResult> results)
        {
            _results =
                results;
        }

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
            float[] queryEmbedding,
            int take,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _results);
        }
    }

    private sealed class FakeAnswerGenerator
        : IAnswerGenerator
    {
        private readonly string
            _answer;

        public FakeAnswerGenerator(
            string answer)
        {
            _answer =
                answer;
        }

        public bool WasCalled { get; private set; }

        public IReadOnlyList<AnswerContextSource>?
            ReceivedSources { get; private set; }

        public Task<string> GenerateAsync(
            string question,
            IReadOnlyList<AnswerContextSource> sources,
            CancellationToken cancellationToken)
        {
            WasCalled =
                true;

            ReceivedSources =
                sources;

            return Task.FromResult(
                _answer);
        }
    }
}