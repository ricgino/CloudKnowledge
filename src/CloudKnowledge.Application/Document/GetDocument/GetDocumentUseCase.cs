using CloudKnowledge.Application.Documents.Access;
using CloudKnowledge.Application.Users;

namespace CloudKnowledge.Application.Documents.GetDocument;

public sealed class GetDocumentUseCase
{
    private readonly IDocumentAccessRepository
        _documentAccessRepository;

    private readonly ICurrentUser
        _currentUser;

    public GetDocumentUseCase(
        IDocumentAccessRepository documentAccessRepository,
        ICurrentUser currentUser)
    {
        _documentAccessRepository =
            documentAccessRepository;

        _currentUser =
            currentUser;
    }

    public async Task<GetDocumentResult?> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var document =
            await _documentAccessRepository
                .GetByIdAsync(
                    userId,
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