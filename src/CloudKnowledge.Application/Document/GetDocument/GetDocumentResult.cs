using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.GetDocument;

public sealed record GetDocumentResult(
    Guid Id,
    string FileName,
    string ContentType,
    DocumentStatus Status,
    bool IsOwner);
