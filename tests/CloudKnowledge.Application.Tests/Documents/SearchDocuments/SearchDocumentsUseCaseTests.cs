using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.SearchDocuments;

namespace CloudKnowledge.Application.Tests.Documents.SearchDocuments;

public sealed class SearchDocumentsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenQueryIsValid_ShouldSearchUsingGeneratedEmbedding()
    {
        var embeddingGenerator =
            new FakeEmbeddingGenerator();

        var searchRepository =
            new FakeSemanticSearchRepository();

        var useCase =
            new SearchDocumentsUseCase(
                embeddingGenerator,
                searchRepository);

        var result =
            await useCase.ExecuteAsync(
                "personal data protection",
                5,
                CancellationToken.None);

        Assert.Single(result);

        Assert.NotNull(
            searchRepository.ReceivedEmbedding);

        Assert.Equal(
            3,
            searchRepository.ReceivedEmbedding.Length);

        Assert.Equal(
            5,
            searchRepository.ReceivedTake);
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

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
            float[] queryEmbedding,
            int take,
            CancellationToken cancellationToken)
        {
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

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAccessibleAsync(
            Guid userId,
            float[] queryEmbedding,
            int take,
            CancellationToken cancellationToken)
        {
            return SearchAsync(
                queryEmbedding,
                take,
                cancellationToken);
        }
    }
}