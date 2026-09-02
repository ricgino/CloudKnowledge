namespace CloudKnowledge.Application.Documents.SearchDocuments;

public sealed class LexicalQuerySyntaxException
    : Exception
{
    public LexicalQuerySyntaxException(
        string message,
        Exception innerException)
        : base(
            message,
            innerException)
    {
    }
}
