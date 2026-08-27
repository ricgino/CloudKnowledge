using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Tests.Documents.AskDocuments;

public sealed class AskDocumentsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSearchReturnsResults_ShouldGenerateAnswerAndSources()
    {
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

        var semanticSearchRepository =
            new FakeSemanticSearchRepository(
                searchResults);

        var searchDocumentsUseCase =
            new SearchDocumentsUseCase(
                new FakeEmbeddingGenerator(),
                semanticSearchRepository,
                new FakeCurrentUser());

        var answerGenerator =
            new FakeAnswerGenerator(
                "Generated answer [S1]");

        var sut =
            new AskDocumentsUseCase(
                searchDocumentsUseCase,
                answerGenerator);

        var result =
            await sut.ExecuteAsync(
                "What does the document say?",
                5,
                CancellationToken.None);

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
            DocumentRetrievalScope.All,
            semanticSearchRepository.ReceivedScope);
    }

    [Fact]
    public async Task ExecuteAsync_TeamScope_ShouldForwardTheSameScopeToSearch()
    {
        var teamId =
            Guid.NewGuid();

        var scope =
            DocumentRetrievalScope.ForTeam(
                teamId,
                includeDescendants: true);

        var semanticSearchRepository =
            new FakeSemanticSearchRepository(
                new[]
                {
                    new SemanticSearchResult(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        0,
                        "Scoped source.",
                        0.1)
                });

        var searchDocumentsUseCase =
            new SearchDocumentsUseCase(
                new FakeEmbeddingGenerator(),
                semanticSearchRepository,
                new FakeCurrentUser());

        var sut =
            new AskDocumentsUseCase(
                searchDocumentsUseCase,
                new FakeAnswerGenerator(
                    "Scoped answer [S1]"));

        await sut.ExecuteAsync(
            "Scoped question",
            5,
            scope,
            CancellationToken.None);

        Assert.Equal(
            scope,
            semanticSearchRepository.ReceivedScope);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoSearchResults_ShouldReturnFallbackWithoutCallingGenerator()
    {
        var semanticSearchRepository =
            new FakeSemanticSearchRepository(
                Array.Empty<SemanticSearchResult>());

        var searchDocumentsUseCase =
            new SearchDocumentsUseCase(
                new FakeEmbeddingGenerator(),
                semanticSearchRepository,
                new FakeCurrentUser());

        var answerGenerator =
            new FakeAnswerGenerator(
                "This must never be returned.");

        var sut =
            new AskDocumentsUseCase(
                searchDocumentsUseCase,
                answerGenerator);

        var scope =
            DocumentRetrievalScope.ForTeam(
                Guid.NewGuid(),
                includeDescendants: false);

        var result =
            await sut.ExecuteAsync(
                "Question with no results",
                5,
                scope,
                CancellationToken.None);

        Assert.Equal(
            "Non sono state trovate informazioni pertinenti nei documenti.",
            result.Answer);

        Assert.Empty(
            result.Sources);

        Assert.False(
            answerGenerator.WasCalled);

        Assert.Equal(
            scope,
            semanticSearchRepository.ReceivedScope);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("     ")]
    public async Task ExecuteAsync_WhenQuestionIsEmpty_ShouldThrowArgumentException(
        string question)
    {
        var sut =
            CreateUseCase();

        var action =
            async () =>
                await sut.ExecuteAsync(
                    question,
                    5,
                    CancellationToken.None);

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
        var sut =
            CreateUseCase();

        var action =
            async () =>
                await sut.ExecuteAsync(
                    "Valid question",
                    take,
                    CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            action);
    }

    private static AskDocumentsUseCase CreateUseCase()
    {
        var searchDocumentsUseCase =
            new SearchDocumentsUseCase(
                new FakeEmbeddingGenerator(),
                new FakeSemanticSearchRepository(
                    Array.Empty<SemanticSearchResult>()),
                new FakeCurrentUser());

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

        public DocumentRetrievalScope? ReceivedScope
        {
            get;
            private set;
        }

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAccessibleAsync(
            Guid userId,
            float[] queryEmbedding,
            int take,
            DocumentRetrievalScope scope,
            CancellationToken cancellationToken)
        {
            ReceivedScope =
                scope;

            return Task.FromResult(
                _results);
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
