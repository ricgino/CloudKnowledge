using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class PdfPigDocumentTextExtractor
    : IPdfNativeTextExtractor
{
    public string Extract(
        Stream content,
        CancellationToken cancellationToken)
    {
        using var pdf =
            PdfDocument.Open(content);

        var text =
            new StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageText =
                ContentOrderTextExtractor.GetText(
                    page,
                    addDoubleNewline: true);

            if (string.IsNullOrWhiteSpace(pageText))
            {
                continue;
            }

            text.AppendLine(pageText);
            text.AppendLine();
        }

        return text.ToString();
    }
}
