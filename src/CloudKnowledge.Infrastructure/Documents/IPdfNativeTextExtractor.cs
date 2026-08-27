namespace CloudKnowledge.Infrastructure.Documents;

public interface IPdfNativeTextExtractor
{
    string Extract(
        Stream content,
        CancellationToken cancellationToken);
}
