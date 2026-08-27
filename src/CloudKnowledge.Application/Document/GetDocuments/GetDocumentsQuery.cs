namespace CloudKnowledge.Application.Documents.GetDocuments;

public sealed record GetDocumentsQuery(
    int Page,
    int PageSize,
    DocumentListScope Scope,
    Guid? TeamId,
    bool IncludeDescendants,
    string? SearchQuery);
