using CloudKnowledge.Application.Documents.ProcessDocument;

namespace CloudKnowledge.Worker;

public sealed class LoggingDocumentProcessingDiagnostics
    : IDocumentProcessingDiagnostics
{
    private readonly ILogger<LoggingDocumentProcessingDiagnostics>
        _logger;

    public LoggingDocumentProcessingDiagnostics(
        ILogger<LoggingDocumentProcessingDiagnostics> logger)
    {
        _logger = logger;
    }

    public void StageStarted(
        Guid documentId,
        string stage)
    {
        _logger.LogInformation(
            "Document {DocumentId} stage {Stage} started.",
            documentId,
            stage);
    }

    public void StageCompleted(
        Guid documentId,
        string stage,
        TimeSpan elapsed)
    {
        _logger.LogInformation(
            "Document {DocumentId} stage {Stage} completed in {ElapsedMilliseconds} ms.",
            documentId,
            stage,
            elapsed.TotalMilliseconds);
    }
}
