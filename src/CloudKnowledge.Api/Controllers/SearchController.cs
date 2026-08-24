using CloudKnowledge.Api.Contracts.Search;
using CloudKnowledge.Application.Documents.SearchDocuments;
using Microsoft.AspNetCore.Mvc;

namespace CloudKnowledge.Api.Controllers;

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
        var results =
            await _searchDocumentsUseCase.ExecuteAsync(
                request.Query,
                request.Take,
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