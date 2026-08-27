using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Tests.Documents.SearchDocuments;

public sealed class SearchDocumentsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_DefaultOverload_ShouldSearchAllAccessibleKnowledge()
    {
        var currentUserId =
            Guid.NewGuid();

        var searchRepository =
            new FakeSemanticSearchRepository();

        var useCase =
            new SearchDocumentsUseCase(
                new FakeEmbeddingGenerator(),
                searchRepository,
                new FakeCurrentUser(
                    currentUserId));

        var result =
            await useCase.ExecuteAsync(
                "personal data protection",
                5,
                CancellationToken.None);

        Assert.Single(
            result);

        Assert.Equal(
            currentUserId,
            searchRepository.ReceivedUserId);

        Assert.NotNull(
            searchRepository.ReceivedEmbedding);

        Assert.Equal(
            3,
            searchRepository.ReceivedEmbedding!.Length);

        Assert.Equal(
            5,
            searchRepository.ReceivedTake);

        Assert.Equal(
            DocumentRetrievalScope.All,
            searchRepository.ReceivedScope);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_TeamScope_ShouldForwardExactScopeToRepository(
        bool includeDescendants)
    {
        var teamId =
            Guid.NewGuid();

        var scope =
            DocumentRetrievalScope.ForTeam(
                teamId,
                includeDescendants);

        var searchRepository =
            new FakeSemanticSearchRepository();

        var useCase =
            new SearchDocumentsUseCase(
                new FakeEmbeddingGenerator(),
                searchRepository,
                new FakeCurrentUser(
                    Guid.NewGuid()));

        await useCase.ExecuteAsync(
            "workflow approval",
            5,
            scope,
            CancellationToken.None);

        Assert.Equal(
            scope,
            searchRepository.ReceivedScope);

        Assert.Equal(
            teamId,
            searchRepository.ReceivedScope?.TeamId);

        Assert.Equal(
            includeDescendants,
            searchRepository.ReceivedScope?.IncludeDescendants);
    }

    private sealed class FakeCurrentUser
        : ICurrentUser
    {
        private readonly Guid
            _userId;

        public FakeCurrentUser(
            Guid userId)
        {
            _userId =
                userId;
        }

        public Task<Guid> GetUserIdAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _userId);
        }
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
            IReadOnlyList<float[]> result =
                new[]
                {
                    new float[]
                    {
                        1,
                        0,
                        0
                    }
                };

            return Task.FromResult(
                result);
        }
    }

    private sealed class FakeSemanticSearchRepository
        : IDocumentSemanticSearchRepository
    {
        public Guid ReceivedUserId
        {
            get;
            private set;
        }

        public float[]? ReceivedEmbedding
        {
            get;
            private set;
        }

        public int ReceivedTake
        {
            get;
            private set;
        }

        public DocumentRetrievalScope? ReceivedScope
        {
            get;
            private set;
        }

        public Task<IReadOnlyList<SemanticSearchResult>>
            SearchAccessibleAsync(
                Guid userId,
                float[] queryEmbedding,
                int take,
                DocumentRetrievalScope scope,
                CancellationToken cancellationToken)
        {
            ReceivedUserId =
                userId;

            ReceivedEmbedding =
                queryEmbedding;

            ReceivedTake =
                take;

            ReceivedScope =
                scope;

            IReadOnlyList<SemanticSearchResult> result =
                new[]
                {
                    new SemanticSearchResult(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        0,
                        "Personal data protection text.",
                        0.1)
                };

            return Task.FromResult(
                result);
        }
    }
}
