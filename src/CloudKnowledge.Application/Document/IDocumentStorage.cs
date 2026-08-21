namespace CloudKnowledge.Application.Documents;

public interface IDocumentStorage
{
    Task UploadAsync(
        Guid documentId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);
}