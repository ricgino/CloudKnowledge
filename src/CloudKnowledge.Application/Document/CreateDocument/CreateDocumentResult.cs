using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.CreateDocument;

public sealed record CreateDocumentResult(
    Guid Id,
    string FileName,
    string ContentType,
    DocumentStatus Status);