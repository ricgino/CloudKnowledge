using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Application.Documents.HybridSearchDocuments;
using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Tests.Documents.AskDocuments;

public sealed class AdjacentAnswerEvidenceTests
{
    [Fact]
    public async Task ExecuteAsync_SelectedChunk_ShouldIncludeAccessibleNextChunkInAnswerEvidence()
    {
        var userId =
            Guid.NewGuid();

        var documentId =
            Guid.NewGuid();

        var selectedChunk =
            new SemanticSearchResult(
                documentId,
                Guid.NewGuid(),
                10,
                "ISLE OF DOGS screenplay. Contents. Cast and Crew. Interview with the writers.",
                0.12);

        var nextChunk =
            new DocumentChunkContextResult(
                documentId,
                Guid.NewGuid(),
                11,
                "Principal Cast\nCHIEF Bryan Cranston\nREX Edward Norton\nBOSS Bill Murray\nDUKE Jeff Goldblum\nKING Bob Balaban");

        var currentUser =
            new FakeCurrentUser(
                userId);

        var semanticRepository =
            new FakeSemanticSearchRepository(
                selectedChunk);

        var searchUseCase =
            new SearchDocumentsUseCase(
                new FakeEmbeddingGenerator(),
                semanticRepository,
                currentUser);

        var hybridUseCase =
            new HybridSearchDocumentsUseCase(
                searchUseCase,
                new ChunkNavigationQualityClassifier());

        var answerGenerator =
            new RecordingAnswerGenerator();

        var contextRepository =
            new FakeDocumentChunkContextRepository(
                nextChunk);

        var queryGenerator =
            new FakeRetrievalQueryGenerator(
                "Isle of Dogs principal cast character actors");

        var sut =
            new AskDocumentsUseCase(
                hybridUseCase,
                answerGenerator,
                queryGenerator,
                currentUser,
                contextRepository);

        var scope =
            DocumentRetrievalScope.ForTeam(
                Guid.NewGuid(),
                includeDescendants: true);

        var result =
            await sut.ExecuteAsync(
                "Quali sono gli attori che doppiano i cani protagonisti del film Isola dei cani?",
                5,
                scope,
                CancellationToken.None);

        Assert.NotNull(
            answerGenerator.ReceivedSources);

        Assert.Contains(
            answerGenerator.ReceivedSources!,
            source =>
                source.Content.Contains(
                    "CHIEF Bryan Cranston",
                    StringComparison.Ordinal)
                && source.Content.Contains(
                    "REX Edward Norton",
                    StringComparison.Ordinal));

        Assert.Contains(
            result.Sources,
            source =>
                source.ChunkId == nextChunk.ChunkId
                && source.Content.Contains(
                    "Principal Cast",
                    StringComparison.Ordinal));

        Assert.Equal(
            userId,
            contextRepository.ReceivedUserId);

        Assert.Equal(
            scope,
            contextRepository.ReceivedScope);

        Assert.Equal(
            documentId,
            contextRepository.ReceivedDocumentId);

        Assert.Equal(
            selectedChunk.Position,
            contextRepository.ReceivedPosition);
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
                inputs
                    .Select(
                        _ =>
                            new[] { 0.1f, 0.2f, 0.3f })
                    .ToArray();

            return Task.FromResult(
                result);
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

            return Task.FromResult(
                results);
        }
    }

    private sealed class FakeCurrentUser(Guid userId)
        : ICurrentUser
    {
        public Task<Guid> GetUserIdAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                userId);
        }
    }

    private sealed class FakeRetrievalQueryGenerator(string query)
        : IRetrievalQueryGenerator
    {
        public Task<IReadOnlyList<string>> GenerateAsync(
            string question,
            int maximumQueries,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<string> result =
                [query];

            return Task.FromResult(
                result);
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
            ReceivedSources =
                sources;

            return Task.FromResult(
                "Grounded answer");
        }
    }

    private sealed class FakeDocumentChunkContextRepository(
        DocumentChunkContextResult nextChunk)
        : IDocumentChunkContextRepository
    {
        public Guid? ReceivedUserId { get; private set; }
        public Guid? ReceivedDocumentId { get; private set; }
        public int? ReceivedPosition { get; private set; }
        public DocumentRetrievalScope? ReceivedScope { get; private set; }

        public Task<DocumentChunkContextResult?> GetAccessibleNextAsync(
            Guid userId,
            Guid documentId,
            int position,
            DocumentRetrievalScope scope,
            CancellationToken cancellationToken)
        {
            ReceivedUserId =
                userId;

            ReceivedDocumentId =
                documentId;

            ReceivedPosition =
                position;

            ReceivedScope =
                scope;

            return Task.FromResult<DocumentChunkContextResult?>(
                nextChunk);
        }
    }
}
