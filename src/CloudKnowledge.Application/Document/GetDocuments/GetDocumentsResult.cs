using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.GetDocuments;

public sealed record GetDocumentsResult(
    IReadOnlyList<GetDocumentsItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record GetDocumentsItem(
    Guid Id,
    string FileName,
    string ContentType,
    DocumentStatus Status,
    bool IsOwner);
