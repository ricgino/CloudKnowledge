using System.IO.Compression;
using System.Text;
using CloudKnowledge.Infrastructure.Documents;

namespace CloudKnowledge.Infrastructure.Tests.Documents;

public sealed class DocumentTextExtractorTests
{
    [Fact]
    public void PlainTextExtractor_ShouldReadUtf8Text()
    {
        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "first line\nsecond line àèìòù"));

        var extractor =
            new PlainTextDocumentTextExtractor();

        var text =
            extractor.Extract(
                stream,
                CancellationToken.None);

        Assert.Contains(
            "first line",
            text);
        Assert.Contains(
            "second line àèìòù",
            text);
    }

    [Fact]
    public void OpenXmlExtractor_ShouldReadParagraphText()
    {
        using var stream =
            CreateDocx(
                "CloudKnowledge handbook",
                "Second paragraph");

        var extractor =
            new OpenXmlDocumentTextExtractor();

        var text =
            extractor.Extract(
                stream,
                CancellationToken.None);

        Assert.Contains(
            "CloudKnowledge handbook",
            text);
        Assert.Contains(
            "Second paragraph",
            text);
    }

    [Fact]
    public void PdfExtractor_ShouldPreferNativeTextWithoutRunningOcr()
    {
        var nativeExtractor =
            new StubNativePdfTextExtractor(
                "native PDF text");

        var ocrExtractor =
            new StubOcrPdfTextExtractor(
                "OCR text");

        var extractor =
            new PdfDocumentTextExtractor(
                nativeExtractor,
                ocrExtractor);

        using var stream =
            new MemoryStream(
                [1, 2, 3, 4]);

        var text =
            extractor.Extract(
                stream,
                CancellationToken.None);

        Assert.Equal(
            "native PDF text",
            text);
        Assert.Equal(
            0,
            ocrExtractor.CallCount);
    }

    [Fact]
    public void PdfExtractor_ShouldFallbackToOcrWhenNativeTextIsEmpty()
    {
        var nativeExtractor =
            new StubNativePdfTextExtractor(
                "   \r\n  ");

        var ocrExtractor =
            new StubOcrPdfTextExtractor(
                "text recovered from scan");

        var extractor =
            new PdfDocumentTextExtractor(
                nativeExtractor,
                ocrExtractor);

        using var stream =
            new MemoryStream(
                [1, 2, 3, 4]);

        var text =
            extractor.Extract(
                stream,
                CancellationToken.None);

        Assert.Equal(
            "text recovered from scan",
            text);
        Assert.Equal(
            1,
            ocrExtractor.CallCount);
        Assert.Equal(
            0,
            ocrExtractor.StreamPositionAtCall);
    }

    private static MemoryStream CreateDocx(
        params string[] paragraphs)
    {
        var stream =
            new MemoryStream();

        using (
            var archive =
                new ZipArchive(
                    stream,
                    ZipArchiveMode.Create,
                    leaveOpen: true))
        {
            WriteEntry(
                archive,
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                </Types>
                """);

            WriteEntry(
                archive,
                "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                </Relationships>
                """);

            var body =
                string.Join(
                    string.Empty,
                    paragraphs.Select(
                        paragraph =>
                            $"<w:p><w:r><w:t>{System.Security.SecurityElement.Escape(paragraph)}</w:t></w:r></w:p>"));

            WriteEntry(
                archive,
                "word/document.xml",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>{body}</w:body>
                </w:document>
                """);
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteEntry(
        ZipArchive archive,
        string path,
        string content)
    {
        var entry =
            archive.CreateEntry(path);

        using var writer =
            new StreamWriter(
                entry.Open(),
                new UTF8Encoding(false));

        writer.Write(content.Trim());
    }

    private sealed class StubNativePdfTextExtractor
        : IPdfNativeTextExtractor
    {
        private readonly string _text;

        public StubNativePdfTextExtractor(
            string text)
        {
            _text = text;
        }

        public string Extract(
            Stream content,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            content.Position = content.Length;
            return _text;
        }
    }

    private sealed class StubOcrPdfTextExtractor
        : IPdfOcrTextExtractor
    {
        private readonly string _text;

        public StubOcrPdfTextExtractor(
            string text)
        {
            _text = text;
        }

        public int CallCount { get; private set; }
        public long StreamPositionAtCall { get; private set; } = -1;

        public string Extract(
            Stream content,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            StreamPositionAtCall = content.Position;
            return _text;
        }
    }
}
