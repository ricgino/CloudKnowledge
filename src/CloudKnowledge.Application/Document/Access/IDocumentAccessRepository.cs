namespace CloudKnowledge.Application.Documents.Access;

public interface IDocumentAccessRepository
{
    Task<bool> CanAccessAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken);
}