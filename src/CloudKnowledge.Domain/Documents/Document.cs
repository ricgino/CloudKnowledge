namespace CloudKnowledge.Domain.Documents;

public sealed class Document
{
    public Guid Id { get; private set; }

    public Guid? OwnerUserId { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public DocumentStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public void AssignOwner(
        Guid ownerUserId)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Owner user id cannot be empty.",
                nameof(ownerUserId));
        }

        if (OwnerUserId == ownerUserId)
        {
            return;
        }

        if (OwnerUserId is not null)
        {
            throw new InvalidOperationException(
                "Document already has an owner.");
        }

        OwnerUserId =
            ownerUserId;
    }

    public void MarkAsProcessing()
    {
        if (Status != DocumentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot start processing a document with status '{Status}'.");
        }

        Status =
            DocumentStatus.Processing;
    }

    public void MarkAsReady()
    {
        if (Status != DocumentStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Cannot mark as ready a document with status '{Status}'.");
        }

        Status =
            DocumentStatus.Ready;
    }

    public void MarkAsFailed()
    {
        if (Status != DocumentStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Cannot mark as failed a document with status '{Status}'.");
        }

        Status =
            DocumentStatus.Failed;
    }

    private Document(
        Guid id,
        string fileName,
        string contentType,
        DocumentStatus status,
        DateTime createdAtUtc)
    {
        Id =
            id;

        FileName =
            fileName;

        ContentType =
            contentType;

        Status =
            status;

        CreatedAtUtc =
            createdAtUtc;
    }

    public static Document Create(
        string fileName,
        string contentType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "File name cannot be empty.",
                nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException(
                "Content type cannot be empty.",
                nameof(contentType));
        }

        return new Document(
            Guid.NewGuid(),
            fileName,
            contentType,
            DocumentStatus.Pending,
            DateTime.UtcNow);
    }
}