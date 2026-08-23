using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.FailDocument;

public sealed class FailDocumentUseCase
{
    private readonly IDocumentRepository _documentRepository;

    public FailDocumentUseCase(
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
            return;
        }

        if (document.Status == DocumentStatus.Failed ||
            document.Status == DocumentStatus.Ready)
        {
            return;
        }

        if (document.Status == DocumentStatus.Pending)
        {
            document.MarkAsProcessing();
        }

        document.MarkAsFailed();

        await _documentRepository.UpdateAsync(
            document,
            cancellationToken);
    }
}
