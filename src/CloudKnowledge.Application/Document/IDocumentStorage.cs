namespace CloudKnowledge.Application.Documents;

public interface IDocumentStorage
{
    Task UploadAsync(
        Guid documentId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        Guid documentId,
        CancellationToken cancellationToken);
}