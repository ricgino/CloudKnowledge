namespace CloudKnowledge.Application.Documents.ProcessDocument.Exceptions;

public sealed class TransientDocumentProcessingException : Exception
{
    public TransientDocumentProcessingException(
        string message)
        : base(message)
    {
    }

    public TransientDocumentProcessingException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
