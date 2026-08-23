using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Application.Documents.ProcessDocument.Exceptions;

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
            throw new PermanentDocumentProcessingException(
                $"Document '{documentId}' was not found.");
        }

        if (document.Status == DocumentStatus.Ready)
        {
            return;
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
            throw new InvalidOperationException(
                $"Document '{documentId}' cannot be processed " +
                $"from status '{document.Status}'.");
        }

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