using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.ProcessDocument;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Application.Documents.ProcessDocument.Exceptions;

namespace CloudKnowledge.Application.Tests.Documents.ProcessDocument;

public sealed class ProcessDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenPending_ShouldMoveDocumentToReady()
    {
        var document =
            Document.Create(
                "architecture.pdf",
                "application/pdf");

        var repository =
            new FakeDocumentRepository(document);

        var useCase =
            CreateUseCase(repository);

        await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.Equal(
            DocumentStatus.Ready,
            document.Status);

        Assert.Equal(
            2,
            repository.UpdateCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyProcessing_ShouldResumeAndMoveToReady()
    {
        var document =
            Document.Create(
                "architecture.pdf",
                "application/pdf");

        document.MarkAsProcessing();

        var repository =
            new FakeDocumentRepository(document);

        var useCase =
            CreateUseCase(repository);

        await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.Equal(
            DocumentStatus.Ready,
            document.Status);

        Assert.Equal(
            1,
            repository.UpdateCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyReady_ShouldDoNothing()
    {
        var document =
            Document.Create(
                "architecture.pdf",
                "application/pdf");

        document.MarkAsProcessing();
        document.MarkAsReady();

        var repository =
            new FakeDocumentRepository(document);

        var useCase =
            CreateUseCase(repository);

        await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.Equal(
            DocumentStatus.Ready,
            document.Status);

        Assert.Equal(
            0,
            repository.UpdateCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocumentDoesNotExist_ShouldThrowPermanentException()
    {
        var repository =
            new FakeDocumentRepository(null);

        var useCase =
            CreateUseCase(repository);

        await Assert.ThrowsAsync<PermanentDocumentProcessingException>(
            () => useCase.ExecuteAsync(
                Guid.NewGuid(),
                CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WhenExtractedTextIsEmpty_ShouldThrowPermanentException()
    {
        var document =
            Document.Create(
                "empty.pdf",
                "application/pdf");

        var repository =
            new FakeDocumentRepository(document);

        var useCase =
            new ProcessDocumentUseCase(
                repository,
                new FakeDocumentStorage(),
                new FakeDocumentTextExtractor(""),
                new FakeDocumentChunkRepository(),
                new TextChunker(),
                new FakeEmbeddingGenerator(),
            new FakeDocumentChunkEmbeddingRepository()
            );

        await Assert.ThrowsAsync<PermanentDocumentProcessingException>(
            () => useCase.ExecuteAsync(
                document.Id,
                CancellationToken.None));

        Assert.Equal(
            DocumentStatus.Processing,
            document.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ShouldCreateEmbeddingForEveryChunk()
    {
        var document =
            Document.Create(
                "architecture.pdf",
                "application/pdf");

        var repository =
            new FakeDocumentRepository(document);

        var chunkRepository =
            new FakeDocumentChunkRepository();

        var embeddingRepository =
            new FakeDocumentChunkEmbeddingRepository();

        var useCase =
            new ProcessDocumentUseCase(
                repository,
                new FakeDocumentStorage(),
                new FakeDocumentTextExtractor(),
                chunkRepository,
                new TextChunker(),
                new FakeEmbeddingGenerator(),
                embeddingRepository);

        await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.NotNull(
            chunkRepository.SavedChunks);

        Assert.NotNull(
            embeddingRepository.SavedEmbeddings);

        Assert.Equal(
            chunkRepository.SavedChunks.Count,
            embeddingRepository.SavedEmbeddings.Count);

        Assert.All(
            embeddingRepository.SavedEmbeddings,
            embedding =>
                Assert.Equal(
                    1536,
                    embedding.Values.Length));
    }

    private static ProcessDocumentUseCase CreateUseCase(
        IDocumentRepository repository)
    {
        return new ProcessDocumentUseCase(
            repository,
            new FakeDocumentStorage(),
            new FakeDocumentTextExtractor(),
            new FakeDocumentChunkRepository(),
            new TextChunker(),
            new FakeEmbeddingGenerator(),
            new FakeDocumentChunkEmbeddingRepository()
            );
    }

    private sealed class FakeDocumentStorage
        : IDocumentStorage
    {
        public Task UploadAsync(
            Guid documentId,
            Stream content,
            string contentType,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            Stream content =
                new MemoryStream(
                    new byte[] { 1, 2, 3, 4 });

            return Task.FromResult(content);
        }
    }

    private sealed class FakeDocumentTextExtractor
        : IDocumentTextExtractor
    {
        private readonly string _text;

        public FakeDocumentTextExtractor(
            string text = "Extracted document text.")
        {
            _text = text;
        }

        public string Extract(
            Stream content,
            CancellationToken cancellationToken)
        {
            return _text;
        }
    }
    private sealed class FakeDocumentRepository
        : IDocumentRepository
    {
        private readonly Document? _document;

        public int UpdateCount { get; private set; }

        public FakeDocumentRepository(
            Document? document)
        {
            _document = document;
        }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            UpdateCount++;

            return Task.CompletedTask;
        }

        public Task<Document?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            if (_document?.Id == id)
            {
                return Task.FromResult<Document?>(
                    _document);
            }

            return Task.FromResult<Document?>(null);
        }

        public Task<IReadOnlyList<Document>> GetPageAsync(
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Document>>(
                Array.Empty<Document>());
        }

        public Task<int> CountAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

    }

    private sealed class FakeDocumentChunkRepository
            : IDocumentChunkRepository
        {
            public IReadOnlyCollection<DocumentChunk>? SavedChunks
            {
                get;
                private set;
            }

            public Task ReplaceForDocumentAsync(
                Guid documentId,
                IReadOnlyCollection<DocumentChunk> chunks,
                CancellationToken cancellationToken)
            {
                SavedChunks = chunks;

                return Task.CompletedTask;
            }
        }

        private sealed class FakeEmbeddingGenerator
            : IEmbeddingGenerator
        {
            public int Dimensions => 1536;

            public Task<IReadOnlyList<float[]>> GenerateAsync(
                IReadOnlyList<string> inputs,
                CancellationToken cancellationToken)
            {
                var result =
                    inputs
                        .Select(
                            _ =>
                            {
                                var vector =
                                    new float[Dimensions];

                                vector[0] = 1;

                                return vector;
                            })
                        .ToArray();

                return Task.FromResult<
                    IReadOnlyList<float[]>>(
                        result);
            }
        }

        private sealed class FakeDocumentChunkEmbeddingRepository
            : IDocumentChunkEmbeddingRepository
        {
            public IReadOnlyCollection<DocumentChunkEmbedding>?
                SavedEmbeddings
            {
                get;
                private set;
            }

            public Task ReplaceForDocumentAsync(
                Guid documentId,
                IReadOnlyCollection<DocumentChunkEmbedding> embeddings,
                CancellationToken cancellationToken)
            {
                SavedEmbeddings =
                    embeddings;

                return Task.CompletedTask;
            }
        }
}