using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CloudKnowledge.Api.Contracts.Documents;

public sealed class CreateDocumentRequest
{
    [Required]
    public IFormFile File { get; init; } = default!;
}