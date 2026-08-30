using CloudKnowledge.Api.Contracts.Ask;
using CloudKnowledge.Api.Contracts.Documents;
using CloudKnowledge.Application.Documents.AskDocuments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace CloudKnowledge.Api.Controllers;

[Authorize]
[RequiredScope("access_as_user")]
[ApiController]
[Route("api/ask")]
public sealed class AskController
    : ControllerBase
{
    private readonly AskDocumentsUseCase
        _askDocumentsUseCase;

    public AskController(
        AskDocumentsUseCase askDocumentsUseCase)
    {
        _askDocumentsUseCase =
            askDocumentsUseCase;
    }

    [HttpPost]
    public async Task<ActionResult<AskDocumentsResponse>> Ask(
        [FromBody] AskDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        if (!RetrievalScopeRequestParser.TryParse(
                request.Scope,
                request.TeamId,
                request.IncludeDescendants,
                out var scope,
                out var errorMessage))
        {
            return BadRequest(
                new
                {
                    message = errorMessage
                });
        }

        var result =
            await _askDocumentsUseCase.ExecuteAsync(
                request.Question,
                request.Take,
                scope,
                cancellationToken);

        var sources =
            result.Sources
                .Select(
                    source =>
                        new AskDocumentSourceResponse(
                            source.Label,
                            source.DocumentId,
                            source.ChunkId,
                            source.Position,
                            source.Content,
                            source.Similarity))
                .ToArray();

        return Ok(
            new AskDocumentsResponse(
                result.Answer,
                sources,
                result.RetrievalQueries));
    }
}
