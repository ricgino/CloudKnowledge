using System.ComponentModel.DataAnnotations;

namespace CloudKnowledge.Api.Contracts.Documents;

public sealed record CreateDocumentRequest(
    [Required]
    [StringLength(255)]
    string FileName,

    [Required]
    [StringLength(100)]
    string ContentType);