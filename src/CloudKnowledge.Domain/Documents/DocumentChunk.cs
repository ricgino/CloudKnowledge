namespace CloudKnowledge.Domain.Documents;

public sealed class DocumentChunk
{
    public Guid Id { get; private set; }

    public Guid DocumentId { get; private set; }

    public int Position { get; private set; }

    public string Content { get; private set; }

    private DocumentChunk(
        Guid id,
        Guid documentId,
        int position,
        string content)
    {
        Id = id;
        DocumentId = documentId;
        Position = position;
        Content = content;
    }

    public static DocumentChunk Create(
        Guid documentId,
        int position,
        string content)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document id cannot be empty.",
                nameof(documentId));
        }

        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                "Position cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(
                "Chunk content cannot be empty.",
                nameof(content));
        }

        return new DocumentChunk(
            Guid.NewGuid(),
            documentId,
            position,
            content.Trim());
    }
}