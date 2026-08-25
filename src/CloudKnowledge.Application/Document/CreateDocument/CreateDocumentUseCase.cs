using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.CreateDocument;

public sealed class CreateDocumentUseCase
{
    private readonly IDocumentRepository
        _documentRepository;

    private readonly IDocumentStorage
        _documentStorage;

    private readonly IDocumentProcessingQueue
        _documentProcessingQueue;

    private readonly ICurrentUser
        _currentUser;

    public CreateDocumentUseCase(
        IDocumentRepository documentRepository,
        IDocumentStorage documentStorage,
        IDocumentProcessingQueue documentProcessingQueue,
        ICurrentUser currentUser)
    {
        _documentRepository =
            documentRepository;

        _documentStorage =
            documentStorage;

        _documentProcessingQueue =
            documentProcessingQueue;

        _currentUser =
            currentUser;
    }

    public async Task<CreateDocumentResult> ExecuteAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var document =
            Document.Create(
                fileName,
                contentType);

        document.AssignOwner(
            userId);

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