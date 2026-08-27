using System.ComponentModel.DataAnnotations;

namespace CloudKnowledge.Api.Contracts.Search;

public sealed class SearchDocumentsRequest
{
    [Required]
    [MinLength(1)]
    public string Query { get; init; } =
        string.Empty;

    [Range(1, 20)]
    public int Take { get; init; } =
        5;

    public string Scope { get; init; } =
        "all";

    public Guid? TeamId { get; init; }

    public bool IncludeDescendants { get; init; }
}
