using System.Text;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class PlainTextDocumentTextExtractor
{
    public string Extract(
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var reader =
            new StreamReader(
                content,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);

        return reader.ReadToEnd();
    }
}
