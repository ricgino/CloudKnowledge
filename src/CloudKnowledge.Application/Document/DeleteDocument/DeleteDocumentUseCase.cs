using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Documents.DeleteDocument;

public interface IDocumentDeletionRepository
{
    Task<bool> DeleteAuthorizedAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken);
}

public interface IDocumentDeletionStorage
{
    Task DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken);
}

public sealed class DeleteDocumentUseCase
{
    private readonly IDocumentDeletionRepository _documentRepository;
    private readonly IDocumentDeletionStorage _documentStorage;
    private readonly ICurrentUser _currentUser;

    public DeleteDocumentUseCase(
        IDocumentDeletionRepository documentRepository,
        IDocumentDeletionStorage documentStorage,
        ICurrentUser currentUser)
    {
        _documentRepository = documentRepository;
        _documentStorage = documentStorage;
        _currentUser = currentUser;
    }

    public async Task<bool> ExecuteAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var deleted =
            await _documentRepository.DeleteAuthorizedAsync(
                userId,
                documentId,
                cancellationToken);

        if (!deleted)
        {
            return false;
        }

        await _documentStorage.DeleteAsync(
            documentId,
            cancellationToken);

        return true;
    }
}
