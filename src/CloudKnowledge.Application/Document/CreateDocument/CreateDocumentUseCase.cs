using CloudKnowledge.Application.Documents;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.CreateDocument;

public sealed class CreateDocumentUseCase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentStorage _documentStorage;
    private readonly IDocumentProcessingQueue _documentProcessingQueue;

    public CreateDocumentUseCase(
        IDocumentRepository documentRepository,
        IDocumentStorage documentStorage,
        IDocumentProcessingQueue documentProcessingQueue)
    {
        _documentRepository = documentRepository;
        _documentStorage = documentStorage;
        _documentProcessingQueue = documentProcessingQueue;
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

        await _documentProcessingQueue.PublishAsync(
            document.Id,
            cancellationToken);

        return new CreateDocumentResult(
            document.Id,
            document.FileName,
            document.ContentType,
            document.Status);
    }
}