namespace CloudKnowledge.Api.Contracts.Documents;

public sealed record GetDocumentsResponse(
    IReadOnlyList<DocumentResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);