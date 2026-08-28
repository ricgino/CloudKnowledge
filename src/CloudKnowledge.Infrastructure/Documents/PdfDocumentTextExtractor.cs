namespace CloudKnowledge.Infrastructure.Documents;

public sealed class PdfDocumentTextExtractor
{
    private readonly IPdfNativeTextExtractor _nativeExtractor;
    private readonly IPdfOcrTextExtractor _ocrExtractor;

    public PdfDocumentTextExtractor(
        IPdfNativeTextExtractor nativeExtractor,
        IPdfOcrTextExtractor ocrExtractor)
    {
        _nativeExtractor = nativeExtractor;
        _ocrExtractor = ocrExtractor;
    }

    public string Extract(
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bufferedContent =
            new MemoryStream();

        content.CopyTo(bufferedContent);
        bufferedContent.Position = 0;

        var nativeText =
            _nativeExtractor.Extract(
                bufferedContent,
                cancellationToken);

        if (!string.IsNullOrWhiteSpace(nativeText))
        {
            return nativeText;
        }

        bufferedContent.Position = 0;

        return _ocrExtractor.Extract(
            bufferedContent,
            cancellationToken);
    }
}
