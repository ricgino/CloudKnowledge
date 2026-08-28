using System.Globalization;
using System.Text;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class TesseractPdfOcrTextExtractor
    : IPdfOcrTextExtractor
{
    private readonly IExternalCommandRunner _commandRunner;
    private readonly string _languages;
    private readonly int _dpi;

    public TesseractPdfOcrTextExtractor(
        IExternalCommandRunner commandRunner,
        string languages,
        int dpi)
    {
        if (string.IsNullOrWhiteSpace(languages))
        {
            throw new ArgumentException(
                "At least one OCR language must be configured.",
                nameof(languages));
        }

        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dpi),
                dpi,
                "OCR DPI must be greater than zero.");
        }

        _commandRunner = commandRunner;
        _languages = languages;
        _dpi = dpi;
    }

    public string Extract(
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workingDirectory =
            Path.Combine(
                Path.GetTempPath(),
                $"cloudknowledge-ocr-{Guid.NewGuid():N}");

        Directory.CreateDirectory(
            workingDirectory);

        try
        {
            var inputPath =
                Path.Combine(
                    workingDirectory,
                    "input.pdf");

            using (
                var inputFile =
                    File.Create(
                        inputPath))
            {
                content.CopyTo(
                    inputFile);
            }

            var pagePrefix =
                Path.Combine(
                    workingDirectory,
                    "page");

            var renderResult =
                _commandRunner.Run(
                    "pdftoppm",
                    [
                        "-png",
                        "-r",
                        _dpi.ToString(
                            CultureInfo.InvariantCulture),
                        inputPath,
                        pagePrefix
                    ],
                    cancellationToken);

            EnsureSuccess(
                "PDF rendering",
                renderResult);

            var pageFiles =
                Directory
                    .GetFiles(
                        workingDirectory,
                        "page-*.png")
                    .OrderBy(
                        GetPageNumber)
                    .ThenBy(
                        path => path,
                        StringComparer.Ordinal)
                    .ToArray();

            if (pageFiles.Length == 0)
            {
                throw new InvalidOperationException(
                    "PDF rendering completed without producing any page images.");
            }

            var text =
                new StringBuilder();

            foreach (var pageFile in pageFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ocrResult =
                    _commandRunner.Run(
                        "tesseract",
                        [
                            pageFile,
                            "stdout",
                            "-l",
                            _languages,
                            "--psm",
                            "3"
                        ],
                        cancellationToken);

                EnsureSuccess(
                    $"OCR for '{Path.GetFileName(pageFile)}'",
                    ocrResult);

                if (string.IsNullOrWhiteSpace(
                        ocrResult.StandardOutput))
                {
                    continue;
                }

                text.AppendLine(
                    ocrResult.StandardOutput.Trim());
                text.AppendLine();
            }

            return text.ToString();
        }
        finally
        {
            TryDeleteDirectory(
                workingDirectory);
        }
    }

    private static void EnsureSuccess(
        string operation,
        ExternalCommandResult result)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        var details =
            string.IsNullOrWhiteSpace(
                result.StandardError)
                ? "No error output was provided."
                : result.StandardError.Trim();

        throw new InvalidOperationException(
            $"{operation} failed with exit code {result.ExitCode}: {details}");
    }

    private static int GetPageNumber(
        string path)
    {
        var name =
            Path.GetFileNameWithoutExtension(
                path);

        var separator =
            name.LastIndexOf('-');

        if (
            separator >= 0 &&
            int.TryParse(
                name[(separator + 1)..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var pageNumber))
        {
            return pageNumber;
        }

        return int.MaxValue;
    }

    private static void TryDeleteDirectory(
        string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(
                    path,
                    recursive: true);
            }
        }
        catch
        {
            // Temporary OCR files are best-effort cleanup only.
        }
    }
}
