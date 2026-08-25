using CloudKnowledge.Api.Contracts.Documents;
using Microsoft.AspNetCore.Mvc;
using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Application.Documents.GetDocument;
using CloudKnowledge.Application.Documents.GetDocuments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web.Resource;
using CloudKnowledge.Application.Documents.Sharing;

namespace CloudKnowledge.Api.Controllers;

[Authorize]
[RequiredScope("access_as_user")]
[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{

    private readonly CreateDocumentUseCase _createDocumentUseCase;
    private readonly GetDocumentUseCase _getDocumentUseCase;
    private readonly GetDocumentsUseCase _getDocumentsUseCase;
    private readonly ShareDocumentWithTeamUseCase _shareDocumentWithTeamUseCase;
    private readonly UnshareDocumentFromTeamUseCase _unshareDocumentFromTeamUseCase;

    public DocumentsController(
        CreateDocumentUseCase createDocumentUseCase,
        GetDocumentUseCase getDocumentUseCase,
        GetDocumentsUseCase getDocumentsUseCase,
        ShareDocumentWithTeamUseCase shareDocumentWithTeamUseCase,
        UnshareDocumentFromTeamUseCase unshareDocumentFromTeamUseCase)
    {
        _createDocumentUseCase = createDocumentUseCase;
        _getDocumentUseCase = getDocumentUseCase;
        _getDocumentsUseCase = getDocumentsUseCase;
        _shareDocumentWithTeamUseCase = shareDocumentWithTeamUseCase;
        _unshareDocumentFromTeamUseCase = unshareDocumentFromTeamUseCase;
    }

[HttpPost]
[Consumes("multipart/form-data")]
public async Task<ActionResult<DocumentResponse>> Create(
    [FromForm] CreateDocumentRequest request,
    CancellationToken cancellationToken)
{
    if (request.File.Length == 0)
    {
        return BadRequest("The uploaded file is empty.");
    }

    await using var stream =
        request.File.OpenReadStream();

    var result = await _createDocumentUseCase.ExecuteAsync(
        request.File.FileName,
        request.File.ContentType,
        stream,
        cancellationToken);

    var response = new DocumentResponse(
        result.Id,
        result.FileName,
        result.ContentType,
        result.Status.ToString());

    return CreatedAtAction(
        nameof(GetById),
        new { id = response.Id },
        response);
}

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _getDocumentUseCase.ExecuteAsync(
            id,
            cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        var response = new DocumentResponse(
            result.Id,
            result.FileName,
            result.ContentType,
            result.Status.ToString());

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<GetDocumentsResponse>> GetAll(
        [FromQuery] GetDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _getDocumentsUseCase.ExecuteAsync(
            request.Page,
            request.PageSize,
            cancellationToken);

        var items = result.Items
            .Select(document => new DocumentResponse(
                document.Id,
                document.FileName,
                document.ContentType,
                document.Status.ToString()))
            .ToList();

        var response = new GetDocumentsResponse(
            items,
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }
    
    [HttpPut("{documentId:guid}/teams/{teamId:guid}")]
    public async Task<IActionResult> ShareWithTeam(
        Guid documentId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var result =
            await _shareDocumentWithTeamUseCase.ExecuteAsync(
                documentId,
                teamId,
                cancellationToken);

        return result switch
        {
            ShareDocumentStatus.Shared =>
                NoContent(),

            ShareDocumentStatus.AlreadyShared =>
                NoContent(),

            ShareDocumentStatus.DocumentNotFoundOrNotOwner =>
                NotFound(),

            ShareDocumentStatus.TeamNotFoundOrNotMember =>
                NotFound(),

            _ =>
                throw new InvalidOperationException(
                    "Unexpected share document result.")
        };
    }

    [HttpDelete("{documentId:guid}/teams/{teamId:guid}")]
    public async Task<IActionResult> UnshareFromTeam(
        Guid documentId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var result =
            await _unshareDocumentFromTeamUseCase.ExecuteAsync(
                documentId,
                teamId,
                cancellationToken);

        return result switch
        {
            UnshareDocumentStatus.Unshared =>
                NoContent(),

            UnshareDocumentStatus.NotShared =>
                NoContent(),

            UnshareDocumentStatus.DocumentNotFoundOrNotOwner =>
                NotFound(),

            UnshareDocumentStatus.TeamNotFoundOrNotMember =>
                NotFound(),

            _ =>
                throw new InvalidOperationException(
                    "Unexpected unshare document result.")
        };
    }
}