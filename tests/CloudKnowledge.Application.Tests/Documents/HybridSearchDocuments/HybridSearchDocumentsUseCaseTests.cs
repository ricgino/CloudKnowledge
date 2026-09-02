using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.HybridSearchDocuments;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Tests.Documents.HybridSearchDocuments;

public sealed class HybridSearchDocumentsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldFuseAndDeduplicateSemanticAndLexicalCandidates()
    {
        var semanticOnly =
            CreateSemanticResult(
                "semantic only",
                cosineDistance: 0.10);

        var both =
            CreateSemanticResult(
                "present in both channels",
                cosineDistance: 0.20);

        var lexicalOnly =
            CreateLexicalResult(
                "lexical only exact technical evidence",
                rank: 0.9);

        var semanticRepository =
            new StubSemanticRepository(
                new[]
                {
                    semanticOnly,
                    both
                });

        var lexicalRepository =
            new StubLexicalRepository(
                new[]
                {
                    new LexicalSearchResult(
                        both.DocumentId,
                        both.ChunkId,
                        both.Position,
                        both.Content,
                        1.0),
                    lexicalOnly
                });

        var currentUser =
            new StubCurrentUser();

        var useCase =
            new HybridSearchDocumentsUseCase(
                new SearchDocumentsUseCase(
                    new StubEmbeddingGenerator(),
                    semanticRepository,
                    currentUser),
                new LexicalSearchDocumentsUseCase(
                    lexicalRepository,
                    currentUser),
                new ChunkNavigationQualityClassifier());

        var response =
            await useCase.ExecuteAsync(
                "technical query",
                10,
                DocumentRetrievalScope.All,
                CancellationToken.None);

        Assert.Equal(
            3,
            response.Results.Count);

        var bothResult =
            Assert.Single(
                response.Results,
                result =>
                    result.ChunkId == both.ChunkId);

        Assert.Equal(
            HybridRetrievalChannel.Both,
            bothResult.Channel);

        Assert.Equal(
            2,
            bothResult.SemanticRank);

        Assert.Equal(
            1,
            bothResult.LexicalRank);

        var semanticOnlyResult =
            Assert.Single(
                response.Results,
                result =>
                    result.ChunkId == semanticOnly.ChunkId);

        Assert.True(
            bothResult.FusedScore >
            semanticOnlyResult.FusedScore);

        var lexicalOnlyResult =
            Assert.Single(
                response.Results,
                result =>
                    result.ChunkId == lexicalOnly.ChunkId);

        Assert.Equal(
            HybridRetrievalChannel.Lexical,
            lexicalOnlyResult.Channel);

        Assert.Null(
            lexicalOnlyResult.CosineDistance);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportChannelDiagnostics()
    {
        var semantic =
            CreateSemanticResult(
                "semantic evidence",
                cosineDistance: 0.15);

        var lexical =
            CreateLexicalResult(
                "lexical evidence",
                rank: 0.8);

        var currentUser =
            new StubCurrentUser();

        var useCase =
            new HybridSearchDocumentsUseCase(
                new SearchDocumentsUseCase(
                    new StubEmbeddingGenerator(),
                    new StubSemanticRepository(
                        new[] { semantic }),
                    currentUser),
                new LexicalSearchDocumentsUseCase(
                    new StubLexicalRepository(
                        new[] { lexical }),
                    currentUser),
                new ChunkNavigationQualityClassifier());

        var response =
            await useCase.ExecuteAsync(
                "technical query",
                10,
                DocumentRetrievalScope.All,
                CancellationToken.None);

        Assert.Single(
            response.Diagnostics.SemanticCandidates);

        Assert.Single(
            response.Diagnostics.LexicalCandidates);

        Assert.Equal(
            2,
            response.Diagnostics.HybridCandidates.Count);
    }

    private static SemanticSearchResult CreateSemanticResult(
        string content,
        double cosineDistance)
    {
        return new SemanticSearchResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            content,
            cosineDistance);
    }

    private static LexicalSearchResult CreateLexicalResult(
        string content,
        double rank)
    {
        return new LexicalSearchResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            content,
            rank);
    }

    private sealed class StubCurrentUser
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

    private sealed class StubEmbeddingGenerator
        : IEmbeddingGenerator
    {
        public int Dimensions => 3;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<float[]> vectors =
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
                vectors);
        }
    }

    private sealed class StubSemanticRepository
        : IDocumentSemanticSearchRepository
    {
        private readonly IReadOnlyList<SemanticSearchResult>
            _results;

        public StubSemanticRepository(
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
                _results);
        }

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAccessibleAsync(
            Guid userId,
            float[] queryEmbedding,
            int take,
            DocumentRetrievalScope scope,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _results);
        }
    }

    private sealed class StubLexicalRepository
        : IDocumentLexicalSearchRepository
    {
        private readonly IReadOnlyList<LexicalSearchResult>
            _results;

        public StubLexicalRepository(
            IReadOnlyList<LexicalSearchResult> results)
        {
            _results = results;
        }

        public Task<IReadOnlyList<LexicalSearchResult>> SearchAccessibleAsync(
            Guid userId,
            string query,
            int take,
            DocumentRetrievalScope scope,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _results);
        }
    }
}
