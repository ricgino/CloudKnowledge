namespace CloudKnowledge.Application.Documents.ProcessDocument;

public interface IDocumentProcessingDiagnostics
{
    void StageStarted(
        Guid documentId,
        string stage);

    void StageCompleted(
        Guid documentId,
        string stage,
        TimeSpan elapsed);
}

public sealed class NullDocumentProcessingDiagnostics
    : IDocumentProcessingDiagnostics
{
    public static NullDocumentProcessingDiagnostics Instance { get; } =
        new();

    private NullDocumentProcessingDiagnostics()
    {
    }

    public void StageStarted(
        Guid documentId,
        string stage)
    {
        _ = documentId;
        _ = stage;
    }

    public void StageCompleted(
        Guid documentId,
        string stage,
        TimeSpan elapsed)
    {
        _ = documentId;
        _ = stage;
        _ = elapsed;
    }
}
