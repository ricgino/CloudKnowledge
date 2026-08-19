using CloudKnowledge.Application.Documents;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.CreateDocument;

public sealed class CreateDocumentUseCase
{
    private readonly IDocumentRepository _documentRepository;

    public CreateDocumentUseCase(
        IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<CreateDocumentResult> ExecuteAsync(
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        var document = Document.Create(
            fileName,
            contentType);

        await _documentRepository.AddAsync(
            document,
            cancellationToken);

        return new CreateDocumentResult(
            document.Id,
            document.FileName,
            document.ContentType,
            document.Status);
    }
}