using CloudKnowledge.Api.Contracts.Ask;
using CloudKnowledge.Application.Documents.AskDocuments;
using Microsoft.AspNetCore.Mvc;

namespace CloudKnowledge.Api.Controllers;

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
        var result =
            await _askDocumentsUseCase.ExecuteAsync(
                request.Question,
                request.Take,
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
                sources));
    }
}