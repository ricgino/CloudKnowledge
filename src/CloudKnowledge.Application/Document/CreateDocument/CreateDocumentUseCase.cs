using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.CreateDocument;

public sealed class CreateDocumentUseCase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentStorage _documentStorage;
    private readonly IDocumentProcessingQueue _documentProcessingQueue;
    private readonly ITeamMembershipRepository _teamMembershipRepository;
    private readonly ICurrentUser _currentUser;

    public CreateDocumentUseCase(
        IDocumentRepository documentRepository,
        IDocumentStorage documentStorage,
        IDocumentProcessingQueue documentProcessingQueue,
        ITeamMembershipRepository teamMembershipRepository,
        ICurrentUser currentUser)
    {
        _documentRepository = documentRepository;
        _documentStorage = documentStorage;
        _documentProcessingQueue = documentProcessingQueue;
        _teamMembershipRepository = teamMembershipRepository;
        _currentUser = currentUser;
    }

    public async Task<CreateDocumentResult> ExecuteAsync(
        string fileName,
        string contentType,
        Stream content,
        Guid? teamId,
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        if (teamId.HasValue)
        {
            var isDirectMember =
                await _teamMembershipRepository.IsMemberAsync(
                    teamId.Value,
                    userId,
                    cancellationToken);

            if (!isDirectMember)
            {
                throw new UnauthorizedAccessException(
                    "The selected team is not available to the current user.");
            }
        }

        var document =
            Document.Create(
                fileName,
                contentType);

        if (teamId.HasValue)
        {
            document.AssignTeamOwner(
                teamId.Value);
        }
        else
        {
            document.AssignOwner(
                userId);
        }

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
