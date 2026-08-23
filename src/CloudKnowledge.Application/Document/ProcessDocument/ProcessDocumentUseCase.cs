using CloudKnowledge.Application.Documents;

namespace CloudKnowledge.Application.Documents.ProcessDocument;

public sealed class ProcessDocumentUseCase
{
    private readonly IDocumentRepository _documentRepository;

    public ProcessDocumentUseCase(
        IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
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
            throw new InvalidOperationException(
                $"Document '{documentId}' was not found.");
        }

        document.MarkAsProcessing();

        await _documentRepository.UpdateAsync(
            document,
            cancellationToken);

        // Real document processing will go here later:
        // text extraction
        // chunking
        // embeddings
        // indexing

        document.MarkAsReady();

        await _documentRepository.UpdateAsync(
            document,
            cancellationToken);
    }
}