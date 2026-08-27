using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class OpenXmlDocumentTextExtractor
{
    public string Extract(
        Stream content,
        CancellationToken cancellationToken)
    {
        using var document =
            WordprocessingDocument.Open(
                content,
                false);

        var body =
            document.MainDocumentPart?
                .Document
                .Body;

        if (body is null)
        {
            return string.Empty;
        }

        var text =
            new StringBuilder();

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var paragraphText =
                paragraph.InnerText;

            if (string.IsNullOrWhiteSpace(paragraphText))
            {
                continue;
            }

            text.AppendLine(paragraphText);
            text.AppendLine();
        }

        return text.ToString();
    }
}
