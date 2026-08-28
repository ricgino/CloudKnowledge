namespace CloudKnowledge.Application.Documents;

public enum SupportedDocumentFormat
{
    Pdf = 1,
    Docx = 2,
    Text = 3
}

public static class DocumentFormatDetector
{
    public static bool TryDetect(
        string fileName,
        out SupportedDocumentFormat format)
    {
        var extension =
            Path.GetExtension(fileName)
                .ToLowerInvariant();

        switch (extension)
        {
            case ".pdf":
                format = SupportedDocumentFormat.Pdf;
                return true;

            case ".docx":
                format = SupportedDocumentFormat.Docx;
                return true;

            case ".txt":
                format = SupportedDocumentFormat.Text;
                return true;

            default:
                format = default;
                return false;
        }
    }
}
