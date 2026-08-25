using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Tests.Documents.SearchDocuments;

public sealed class SearchDocumentsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenQueryIsValid_ShouldSearchOnlyForCurrentUser()
    {
        var currentUserId =
            Guid.NewGuid();

        var embeddingGenerator =
            new FakeEmbeddingGenerator();

        var searchRepository =
            new FakeSemanticSearchRepository();

        var useCase =
            new SearchDocumentsUseCase(
                embeddingGenerator,
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

        Assert.NotNull(
            searchRepository.ReceivedEmbedding);

        Assert.Equal(
            3,
            searchRepository.ReceivedEmbedding.Length);

        Assert.Equal(
            5,
            searchRepository.ReceivedTake);

        Assert.Equal(
            currentUserId,
            searchRepository.ReceivedUserId);
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

        public Task<IReadOnlyList<SemanticSearchResult>>
            SearchAccessibleAsync(
                Guid userId,
                float[] queryEmbedding,
                int take,
                CancellationToken cancellationToken)
        {
            ReceivedUserId =
                userId;

            ReceivedEmbedding =
                queryEmbedding;

            ReceivedTake =
                take;

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