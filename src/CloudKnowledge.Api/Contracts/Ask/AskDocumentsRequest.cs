using System.ComponentModel.DataAnnotations;

namespace CloudKnowledge.Api.Contracts.Ask;

public sealed class AskDocumentsRequest
{
    [Required]
    [MinLength(1)]
    public string Question { get; init; } =
        string.Empty;

    [Range(1, 10)]
    public int Take { get; init; } =
        5;

    public string Scope { get; init; } =
        "all";

    public Guid? TeamId { get; init; }

    public bool IncludeDescendants { get; init; }
}
