using CloudKnowledge.Infrastructure.Documents;

namespace CloudKnowledge.Infrastructure.Tests.Documents;

public sealed class TesseractPdfOcrTextExtractorTests
{
    [Fact]
    public void Extract_ShouldRenderPagesAndOcrThemInPageOrder()
    {
        var runner =
            new RecordingExternalCommandRunner();

        var extractor =
            new TesseractPdfOcrTextExtractor(
                runner,
                languages: "eng+ita",
                dpi: 300);

        using var content =
            new MemoryStream(
                [1, 2, 3, 4]);

        var text =
            extractor.Extract(
                content,
                CancellationToken.None);

        Assert.Contains(
            "first OCR page",
            text);
        Assert.Contains(
            "second OCR page",
            text);
        Assert.True(
            text.IndexOf(
                "first OCR page",
                StringComparison.Ordinal) <
            text.IndexOf(
                "second OCR page",
                StringComparison.Ordinal));

        var renderCommand =
            Assert.Single(
                runner.Commands.Where(
                    command =>
                        command.FileName == "pdftoppm"));

        Assert.Contains(
            "-png",
            renderCommand.Arguments);
        Assert.Contains(
            "300",
            renderCommand.Arguments);

        var ocrCommands =
            runner.Commands
                .Where(command =>
                    command.FileName == "tesseract")
                .ToArray();

        Assert.Equal(
            2,
            ocrCommands.Length);

        Assert.All(
            ocrCommands,
            command =>
            {
                Assert.Contains(
                    "stdout",
                    command.Arguments);
                Assert.Contains(
                    "eng+ita",
                    command.Arguments);
            });
    }

    private sealed class RecordingExternalCommandRunner
        : IExternalCommandRunner
    {
        public List<RecordedCommand> Commands { get; } = [];

        public ExternalCommandResult Run(
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Commands.Add(
                new RecordedCommand(
                    fileName,
                    arguments.ToArray()));

            if (fileName == "pdftoppm")
            {
                var outputPrefix =
                    arguments[^1];

                File.WriteAllBytes(
                    $"{outputPrefix}-2.png",
                    [2]);
                File.WriteAllBytes(
                    $"{outputPrefix}-1.png",
                    [1]);

                return new ExternalCommandResult(
                    0,
                    string.Empty,
                    string.Empty);
            }

            if (fileName == "tesseract")
            {
                var pagePath =
                    arguments[0];

                var output =
                    pagePath.EndsWith(
                        "-1.png",
                        StringComparison.Ordinal)
                        ? "first OCR page"
                        : "second OCR page";

                return new ExternalCommandResult(
                    0,
                    output,
                    string.Empty);
            }

            throw new InvalidOperationException(
                $"Unexpected command '{fileName}'.");
        }
    }

    private sealed record RecordedCommand(
        string FileName,
        IReadOnlyList<string> Arguments);
}
