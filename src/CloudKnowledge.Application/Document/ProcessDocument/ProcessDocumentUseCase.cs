using System.Diagnostics;
using CloudKnowledge.Application.Documents.ProcessDocument.Exceptions;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.ProcessDocument;

public sealed class ProcessDocumentUseCase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentStorage _documentStorage;
    private readonly IDocumentTextExtractor _documentTextExtractor;
    private readonly IDocumentChunkRepository _documentChunkRepository;
    private readonly TextChunker _textChunker;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IDocumentChunkEmbeddingRepository
        _documentChunkEmbeddingRepository;
    private readonly IDocumentProcessingDiagnostics _diagnostics;

    public ProcessDocumentUseCase(
        IDocumentRepository documentRepository,
        IDocumentStorage documentStorage,
        IDocumentTextExtractor documentTextExtractor,
        IDocumentChunkRepository documentChunkRepository,
        TextChunker textChunker,
        IEmbeddingGenerator embeddingGenerator,
        IDocumentChunkEmbeddingRepository documentChunkEmbeddingRepository,
        IDocumentProcessingDiagnostics? diagnostics = null)
    {
        _documentRepository =
            documentRepository;

        _documentStorage =
            documentStorage;

        _documentTextExtractor =
            documentTextExtractor;

        _documentChunkRepository =
            documentChunkRepository;

        _textChunker =
            textChunker;

        _embeddingGenerator =
            embeddingGenerator;

        _documentChunkEmbeddingRepository =
            documentChunkEmbeddingRepository;

        _diagnostics =
            diagnostics
            ?? NullDocumentProcessingDiagnostics.Instance;
    }

    public async Task ExecuteAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document =
            await _documentRepository.GetByIdAsync(
                documentId,
                cancellationToken);

        if (document is null)
        {
            throw new PermanentDocumentProcessingException(
                $"Document '{documentId}' was not found.");
        }

        if (document.Status == DocumentStatus.Ready)
        {
            return;
        }

        if (!DocumentFormatDetector.TryDetect(
                document.FileName,
                out _))
        {
            throw new PermanentDocumentProcessingException(
                $"Document format for '{document.FileName}' is not supported.");
        }

        if (document.Status == DocumentStatus.Pending)
        {
            document.MarkAsProcessing();

            await _documentRepository.UpdateAsync(
                document,
                cancellationToken);
        }
        else if (document.Status != DocumentStatus.Processing)
        {
            throw new PermanentDocumentProcessingException(
                $"Document '{documentId}' cannot be processed " +
                $"from status '{document.Status}'.");
        }

        await using var blobContent =
            await TraceAsync(
                document.Id,
                "blob-open",
                () =>
                    _documentStorage.OpenReadAsync(
                        document.Id,
                        cancellationToken));

        await using var bufferedContent =
            new MemoryStream();

        await TraceAsync(
            document.Id,
            "blob-copy",
            () =>
                blobContent.CopyToAsync(
                    bufferedContent,
                    cancellationToken));

        bufferedContent.Position = 0;

        string extractedText;

        try
        {
            extractedText =
                Trace(
                    document.Id,
                    "text-extract",
                    () =>
                        _documentTextExtractor.Extract(
                            document.FileName,
                            document.ContentType,
                            bufferedContent,
                            cancellationToken));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PermanentDocumentProcessingException(
                $"Text extraction failed for document '{document.Id}'.",
                exception);
        }

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new PermanentDocumentProcessingException(
                $"Document '{document.Id}' contains no extractable text.");
        }

        var chunkContents =
            Trace(
                document.Id,
                "chunk",
                () =>
                    _textChunker.Chunk(
                        extractedText));

        if (chunkContents.Count == 0)
        {
            throw new PermanentDocumentProcessingException(
                $"Document '{document.Id}' produced no text chunks.");
        }

        var chunks =
            chunkContents
                .Select(
                    (content, position) =>
                        DocumentChunk.Create(
                            document.Id,
                            position,
                            content))
                .ToArray();

        var embeddingVectors =
            await TraceAsync(
                document.Id,
                "embeddings",
                () =>
                    _embeddingGenerator.GenerateAsync(
                        chunks
                            .Select(
                                chunk =>
                                    chunk.Content)
                            .ToArray(),
                        cancellationToken));

        if (embeddingVectors.Count != chunks.Length)
        {
            throw new InvalidOperationException(
                "The embedding generator returned " +
                "an unexpected number of embeddings.");
        }

        if (embeddingVectors.Any(
            embedding =>
                embedding.Length !=
                _embeddingGenerator.Dimensions))
        {
            throw new InvalidOperationException(
                "The embedding generator returned " +
                "an embedding with an invalid dimension.");
        }

        var embeddings =
            chunks
                .Select(
                    (chunk, index) =>
                        new DocumentChunkEmbedding(
                            chunk.Id,
                            document.Id,
                            embeddingVectors[index]))
                .ToArray();

        await TraceAsync(
            document.Id,
            "save-chunks",
            () =>
                _documentChunkRepository.ReplaceForDocumentAsync(
                    document.Id,
                    chunks,
                    cancellationToken));

        await TraceAsync(
            document.Id,
            "save-embeddings",
            () =>
                _documentChunkEmbeddingRepository.ReplaceForDocumentAsync(
                    document.Id,
                    embeddings,
                    cancellationToken));

        await TraceAsync(
            document.Id,
            "mark-ready",
            async () =>
            {
                document.MarkAsReady();

                await _documentRepository.UpdateAsync(
                    document,
                    cancellationToken);
            });
    }

    private async Task<T> TraceAsync<T>(
        Guid documentId,
        string stage,
        Func<Task<T>> operation)
    {
        _diagnostics.StageStarted(
            documentId,
            stage);

        var startedAt =
            Stopwatch.GetTimestamp();

        var result =
            await operation();

        _diagnostics.StageCompleted(
            documentId,
            stage,
            Stopwatch.GetElapsedTime(
                startedAt));

        return result;
    }

    private async Task TraceAsync(
        Guid documentId,
        string stage,
        Func<Task> operation)
    {
        _diagnostics.StageStarted(
            documentId,
            stage);

        var startedAt =
            Stopwatch.GetTimestamp();

        await operation();

        _diagnostics.StageCompleted(
            documentId,
            stage,
            Stopwatch.GetElapsedTime(
                startedAt));
    }

    private T Trace<T>(
        Guid documentId,
        string stage,
        Func<T> operation)
    {
        _diagnostics.StageStarted(
            documentId,
            stage);

        var startedAt =
            Stopwatch.GetTimestamp();

        var result =
            operation();

        _diagnostics.StageCompleted(
            documentId,
            stage,
            Stopwatch.GetElapsedTime(
                startedAt));

        return result;
    }
}
