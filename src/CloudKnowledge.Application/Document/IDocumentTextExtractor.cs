namespace CloudKnowledge.Application.Documents;

public interface IDocumentTextExtractor
{
    string Extract(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);
}
