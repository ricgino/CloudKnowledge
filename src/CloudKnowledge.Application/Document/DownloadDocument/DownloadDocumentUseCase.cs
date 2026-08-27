using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Documents.DownloadDocument;

public sealed record DownloadDocumentResult(
    Stream Content,
    string FileName,
    string ContentType);

public sealed class DownloadDocumentUseCase
{
    private readonly IDocumentAccessRepository _documentAccessRepository;
    private readonly IDocumentStorage _documentStorage;
    private readonly ICurrentUser _currentUser;

    public DownloadDocumentUseCase(
        IDocumentAccessRepository documentAccessRepository,
        IDocumentStorage documentStorage,
        ICurrentUser currentUser)
    {
        _documentAccessRepository = documentAccessRepository;
        _documentStorage = documentStorage;
        _currentUser = currentUser;
    }

    public async Task<DownloadDocumentResult?> ExecuteAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var document =
            await _documentAccessRepository.GetByIdAsync(
                userId,
                documentId,
                cancellationToken);

        if (document is null)
        {
            return null;
        }

        var content =
            await _documentStorage.OpenReadAsync(
                documentId,
                cancellationToken);

        return new DownloadDocumentResult(
            content,
            document.FileName,
            document.ContentType);
    }
}
