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
    
    public ProcessDocumentUseCase(
        IDocumentRepository documentRepository,
        IDocumentStorage documentStorage,
        IDocumentTextExtractor documentTextExtractor,
        IDocumentChunkRepository documentChunkRepository,
        TextChunker textChunker)
    {
        _documentRepository = documentRepository;
        _documentStorage = documentStorage;
        _documentTextExtractor = documentTextExtractor;
        _documentChunkRepository = documentChunkRepository;
        _textChunker = textChunker;
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

        if (!string.Equals(
            document.ContentType,
            "application/pdf",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new PermanentDocumentProcessingException(
                $"Content type '{document.ContentType}' is not supported.");
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
            await _documentStorage.OpenReadAsync(
                document.Id,
                cancellationToken);

        /*
         * For now we buffer the PDF in memory.
         *
         * This gives PdfPig a seekable stream and, importantly,
         * separates Blob/network failures from PDF parsing failures.
         *
         * Later we will add upload/file-size limits.
         */
        await using var bufferedContent =
            new MemoryStream();

        await blobContent.CopyToAsync(
            bufferedContent,
            cancellationToken);

        bufferedContent.Position = 0;

        string extractedText;

        try
        {
            extractedText =
                _documentTextExtractor.Extract(
                    bufferedContent,
                    cancellationToken);
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
            _textChunker.Chunk(
                extractedText);

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

        await _documentChunkRepository.ReplaceForDocumentAsync(
            document.Id,
            chunks,
            cancellationToken);

        document.MarkAsReady();

        await _documentRepository.UpdateAsync(
            document,
            cancellationToken);
    }
}