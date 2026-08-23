namespace CloudKnowledge.Application.Documents.ProcessDocument.Exceptions;

public sealed class PermanentDocumentProcessingException : Exception
{
    public PermanentDocumentProcessingException(
        string message)
        : base(message)
    {
    }

    public PermanentDocumentProcessingException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
