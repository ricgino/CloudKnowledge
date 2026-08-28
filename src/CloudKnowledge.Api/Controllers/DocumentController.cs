using CloudKnowledge.Api.Contracts.Documents;
using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Application.Documents.DeleteDocument;
using CloudKnowledge.Application.Documents.DownloadDocument;
using CloudKnowledge.Application.Documents.GetDocument;
using CloudKnowledge.Application.Documents.GetDocuments;
using CloudKnowledge.Application.Documents.Sharing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

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
    private readonly DeleteDocumentUseCase _deleteDocumentUseCase;
    private readonly DownloadDocumentUseCase _downloadDocumentUseCase;

    public DocumentsController(
        CreateDocumentUseCase createDocumentUseCase,
        GetDocumentUseCase getDocumentUseCase,
        GetDocumentsUseCase getDocumentsUseCase,
        ShareDocumentWithTeamUseCase shareDocumentWithTeamUseCase,
        UnshareDocumentFromTeamUseCase unshareDocumentFromTeamUseCase,
        DeleteDocumentUseCase deleteDocumentUseCase,
        DownloadDocumentUseCase downloadDocumentUseCase)
    {
        _createDocumentUseCase = createDocumentUseCase;
        _getDocumentUseCase = getDocumentUseCase;
        _getDocumentsUseCase = getDocumentsUseCase;
        _shareDocumentWithTeamUseCase = shareDocumentWithTeamUseCase;
        _unshareDocumentFromTeamUseCase = unshareDocumentFromTeamUseCase;
        _deleteDocumentUseCase = deleteDocumentUseCase;
        _downloadDocumentUseCase = downloadDocumentUseCase;
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

        if (!DocumentFormatDetector.TryDetect(
                request.File.FileName,
                out _))
        {
            return BadRequest(new
            {
                message = "Supported document formats are PDF, DOCX and TXT."
            });
        }

        await using var stream =
            request.File.OpenReadStream();

        CreateDocumentResult result;

        try
        {
            result = await _createDocumentUseCase.ExecuteAsync(
                request.File.FileName,
                request.File.ContentType,
                stream,
                request.TeamId,
                cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }

        var isOwner =
            !request.TeamId.HasValue;

        var response = new DocumentResponse(
            result.Id,
            result.FileName,
            result.ContentType,
            result.Status.ToString(),
            isOwner,
            isOwner);

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
            result.Status.ToString(),
            result.IsOwner,
            result.IsOwner);

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<GetDocumentsResponse>> GetAll(
        [FromQuery] GetDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseScope(
                request.Scope,
                out var scope))
        {
            return BadRequest(new
            {
                message = "Scope must be one of: all, owned, team."
            });
        }

        if (scope == DocumentListScope.Team &&
            !request.TeamId.HasValue)
        {
            return BadRequest(new
            {
                message = "teamId is required when scope=team."
            });
        }

        if (scope != DocumentListScope.Team &&
            request.TeamId.HasValue)
        {
            return BadRequest(new
            {
                message = "teamId is valid only when scope=team."
            });
        }

        if (scope != DocumentListScope.Team &&
            request.IncludeDescendants)
        {
            return BadRequest(new
            {
                message = "includeDescendants is valid only when scope=team."
            });
        }

        GetDocumentsResult result;

        try
        {
            result =
                await _getDocumentsUseCase.ExecuteAsync(
                    new GetDocumentsQuery(
                        request.Page,
                        request.PageSize,
                        scope,
                        request.TeamId,
                        request.IncludeDescendants,
                        request.Query),
                    cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }

        var items = result.Items
            .Select(document => new DocumentResponse(
                document.Id,
                document.FileName,
                document.ContentType,
                document.Status.ToString(),
                document.IsOwner,
                document.CanDelete,
                document.SharedTeams
                    .Select(team =>
                        new DocumentAccessTeamResponse(
                            team.Id,
                            team.Name,
                            team.Path))
                    .ToArray()))
            .ToList();

        var response = new GetDocumentsResponse(
            items,
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    [HttpGet("{documentId:guid}/download")]
    public async Task<IActionResult> Download(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var result =
            await _downloadDocumentUseCase.ExecuteAsync(
                documentId,
                cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return File(
            result.Content,
            result.ContentType,
            result.FileName,
            enableRangeProcessing: true);
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var deleted =
            await _deleteDocumentUseCase.ExecuteAsync(
                documentId,
                cancellationToken);

        return deleted
            ? NoContent()
            : NotFound();
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
            ShareDocumentStatus.Shared => NoContent(),
            ShareDocumentStatus.AlreadyShared => NoContent(),
            ShareDocumentStatus.DocumentNotFoundOrNotOwner => NotFound(),
            ShareDocumentStatus.TeamNotFoundOrNotMember => NotFound(),
            _ => throw new InvalidOperationException(
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
            UnshareDocumentStatus.Unshared => NoContent(),
            UnshareDocumentStatus.NotShared => NoContent(),
            UnshareDocumentStatus.DocumentNotFoundOrNotOwner => NotFound(),
            UnshareDocumentStatus.TeamNotFoundOrNotMember => NotFound(),
            _ => throw new InvalidOperationException(
                "Unexpected unshare document result.")
        };
    }

    private static bool TryParseScope(
        string? rawScope,
        out DocumentListScope scope)
    {
        switch (rawScope?.Trim().ToLowerInvariant())
        {
            case "all":
                scope = DocumentListScope.All;
                return true;

            case "owned":
                scope = DocumentListScope.Owned;
                return true;

            case "team":
                scope = DocumentListScope.Team;
                return true;

            default:
                scope = default;
                return false;
        }
    }
}
