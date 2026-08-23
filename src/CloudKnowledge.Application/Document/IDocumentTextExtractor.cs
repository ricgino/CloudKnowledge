namespace CloudKnowledge.Application.Documents;

public interface IDocumentTextExtractor
{
    string Extract(
        Stream content,
        CancellationToken cancellationToken);
}