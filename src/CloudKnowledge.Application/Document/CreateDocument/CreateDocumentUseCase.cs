using CloudKnowledge.Application.Documents;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.CreateDocument;

public sealed class CreateDocumentUseCase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentStorage _documentStorage;

    public CreateDocumentUseCase(
        IDocumentRepository documentRepository,
        IDocumentStorage documentStorage)
    {
        _documentRepository = documentRepository;
        _documentStorage = documentStorage;
    }

    public async Task<CreateDocumentResult> ExecuteAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var document = Document.Create(
            fileName,
            contentType);

        await _documentStorage.UploadAsync(
            document.Id,
            content,
            document.ContentType,
            cancellationToken);

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