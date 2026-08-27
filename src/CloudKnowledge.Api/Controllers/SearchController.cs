using CloudKnowledge.Api.Contracts.Documents;
using CloudKnowledge.Api.Contracts.Search;
using CloudKnowledge.Application.Documents.SearchDocuments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace CloudKnowledge.Api.Controllers;

[Authorize]
[RequiredScope("access_as_user")]
[ApiController]
[Route("api/search")]
public sealed class SearchController
    : ControllerBase
{
    private readonly SearchDocumentsUseCase
        _searchDocumentsUseCase;

    public SearchController(
        SearchDocumentsUseCase searchDocumentsUseCase)
    {
        _searchDocumentsUseCase =
            searchDocumentsUseCase;
    }

    [HttpPost]
    public async Task<
        ActionResult<IReadOnlyList<SearchDocumentResultResponse>>>
        Search(
            [FromBody] SearchDocumentsRequest request,
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

        var results =
            await _searchDocumentsUseCase.ExecuteAsync(
                request.Query,
                request.Take,
                scope,
                cancellationToken);

        var response =
            results
                .Select(
                    result =>
                        new SearchDocumentResultResponse(
                            result.DocumentId,
                            result.ChunkId,
                            result.Position,
                            result.Content,
                            1.0 -
                            result.CosineDistance))
                .ToArray();

        return Ok(response);
    }
}
