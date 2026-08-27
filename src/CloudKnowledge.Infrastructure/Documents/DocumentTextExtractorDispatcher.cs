using CloudKnowledge.Application.Documents;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class DocumentTextExtractorDispatcher
    : IDocumentTextExtractor
{
    private readonly PdfPigDocumentTextExtractor
        _pdfExtractor;

    private readonly OpenXmlDocumentTextExtractor
        _docxExtractor;

    private readonly PlainTextDocumentTextExtractor
        _textExtractor;

    public DocumentTextExtractorDispatcher(
        PdfPigDocumentTextExtractor pdfExtractor,
        OpenXmlDocumentTextExtractor docxExtractor,
        PlainTextDocumentTextExtractor textExtractor)
    {
        _pdfExtractor =
            pdfExtractor;
        _docxExtractor =
            docxExtractor;
        _textExtractor =
            textExtractor;
    }

    public string Extract(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        _ = contentType;

        if (!DocumentFormatDetector.TryDetect(
                fileName,
                out var format))
        {
            throw new NotSupportedException(
                $"Document format for '{fileName}' is not supported.");
        }

        return format switch
        {
            SupportedDocumentFormat.Pdf =>
                _pdfExtractor.Extract(
                    content,
                    cancellationToken),

            SupportedDocumentFormat.Docx =>
                _docxExtractor.Extract(
                    content,
                    cancellationToken),

            SupportedDocumentFormat.Text =>
                _textExtractor.Extract(
                    content,
                    cancellationToken),

            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                "Unknown document format.")
        };
    }
}
