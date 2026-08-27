using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.ProcessDocument;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Tests.Documents.ProcessDocument;

public sealed class NonPdfDocumentProcessingTests
{
    [Theory]
    [InlineData(
        "handbook.docx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData(
        "notes.txt",
        "text/plain")]
    public async Task ExecuteAsync_WhenSupportedNonPdfDocument_ShouldMoveToReady(
        string fileName,
        string contentType)
    {
        var document =
            Document.Create(
                fileName,
                contentType);

        var repository =
            new FakeDocumentRepository(document);

        var useCase =
            new ProcessDocumentUseCase(
                repository,
                new FakeDocumentStorage(),
                new FakeDocumentTextExtractor(),
                new FakeDocumentChunkRepository(),
                new TextChunker(),
                new FakeEmbeddingGenerator(),
                new FakeDocumentChunkEmbeddingRepository());

        await useCase.ExecuteAsync(
            document.Id,
            CancellationToken.None);

        Assert.Equal(
            DocumentStatus.Ready,
            document.Status);
    }

    private sealed class FakeDocumentStorage
        : IDocumentStorage
    {
        public Task UploadAsync(
            Guid documentId,
            Stream content,
            string contentType,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<Stream> OpenReadAsync(
            Guid documentId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(
                new MemoryStream(
                    new byte[] { 1, 2, 3, 4 }));
    }

    private sealed class FakeDocumentTextExtractor
        : IDocumentTextExtractor
    {
        public string Extract(
            Stream content,
            CancellationToken cancellationToken) =>
            "Enterprise document text.";
    }

    private sealed class FakeDocumentRepository
        : IDocumentRepository
    {
        private readonly Document _document;

        public FakeDocumentRepository(
            Document document)
        {
            _document = document;
        }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateAsync(
            Document document,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<Document?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<Document?>(
                _document.Id == id
                    ? _document
                    : null);

        public Task<IReadOnlyList<Document>> GetPageAsync(
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Document>>(
                Array.Empty<Document>());

        public Task<int> CountAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(0);
    }

    private sealed class FakeDocumentChunkRepository
        : IDocumentChunkRepository
    {
        public Task ReplaceForDocumentAsync(
            Guid documentId,
            IReadOnlyCollection<DocumentChunk> chunks,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeEmbeddingGenerator
        : IEmbeddingGenerator
    {
        public int Dimensions => 1536;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<float[]>>(
                inputs
                    .Select(_ => new float[Dimensions])
                    .ToArray());
    }

    private sealed class FakeDocumentChunkEmbeddingRepository
        : IDocumentChunkEmbeddingRepository
    {
        public Task ReplaceForDocumentAsync(
            Guid documentId,
            IReadOnlyCollection<DocumentChunkEmbedding> embeddings,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
