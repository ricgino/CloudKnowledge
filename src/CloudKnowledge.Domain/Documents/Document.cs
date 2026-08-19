namespace CloudKnowledge.Domain.Documents;

public sealed class Document
{
    public Guid Id { get; }

    public string FileName { get; }

    public string ContentType { get; }

    public DocumentStatus Status { get; private set; }

    private Document(
        Guid id,
        string fileName,
        string contentType,
        DocumentStatus status)
    {
        Id = id;
        FileName = fileName;
        ContentType = contentType;
        Status = status;
    }

    public static Document Create(
        string fileName,
        string contentType)
    {
        return new Document(
            Guid.NewGuid(),
            fileName,
            contentType,
            DocumentStatus.Pending);
    }
}