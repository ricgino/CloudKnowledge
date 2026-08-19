namespace CloudKnowledge.Application.Documents.GetDocument;

public sealed class GetDocumentUseCase
{
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentUseCase(
        IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<GetDocumentResult?> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (document is null)
        {
            return null;
        }

        return new GetDocumentResult(
            document.Id,
            document.FileName,
            document.ContentType,
            document.Status);
    }
}