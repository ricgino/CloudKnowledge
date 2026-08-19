using System.ComponentModel.DataAnnotations;

namespace CloudKnowledge.Api.Contracts.Documents;

public sealed class GetDocumentsRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}