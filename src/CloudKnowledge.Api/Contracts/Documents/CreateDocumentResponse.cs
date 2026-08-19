namespace CloudKnowledge.Api.Contracts.Documents;

public sealed record CreateDocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    string Status);