namespace CloudKnowledge.Infrastructure.Documents;

public interface IPdfOcrTextExtractor
{
    string Extract(
        Stream content,
        CancellationToken cancellationToken);
}
