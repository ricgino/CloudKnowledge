using CloudKnowledge.Api.Contracts.Documents;
using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Application.Documents.DeleteDocument;
using CloudKnowledge.Application.Documents.DownloadDocument;
using CloudKnowledge.Application.Documents.GetDocument;
using CloudKnowledge.Application.Documents.GetDocuments;
using CloudKnowledge.Application.Documents.Sharing;
using CloudKnowledge.Application.Teams;
using CloudKnowledge.Application.Users;
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
    private readonly ITeamMembershipRepository _teamMembershipRepository;
    private readonly ICurrentUser _currentUser;

    public DocumentsController(
        CreateDocumentUseCase createDocumentUseCase,
        GetDocumentUseCase getDocumentUseCase,
        GetDocumentsUseCase getDocumentsUseCase,
        ShareDocumentWithTeamUseCase shareDocumentWithTeamUseCase,
        UnshareDocumentFromTeamUseCase unshareDocumentFromTeamUseCase,
        DeleteDocumentUseCase deleteDocumentUseCase,
        DownloadDocumentUseCase downloadDocumentUseCase,
        ITeamMembershipRepository teamMembershipRepository,
        ICurrentUser currentUser)
    {
        _createDocumentUseCase = createDocumentUseCase;
        _getDocumentUseCase = getDocumentUseCase;
        _getDocumentsUseCase = getDocumentsUseCase;
        _shareDocumentWithTeamUseCase = shareDocumentWithTeamUseCase;
        _unshareDocumentFromTeamUseCase = unshareDocumentFromTeamUseCase;
        _deleteDocumentUseCase = deleteDocumentUseCase;
        _downloadDocumentUseCase = downloadDocumentUseCase;
        _teamMembershipRepository = teamMembershipRepository;
        _currentUser = currentUser;
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

        if (request.TeamId.HasValue)
        {
            var userId =
                await _currentUser.GetUserIdAsync(
                    cancellationToken);

            var isTeamMember =
                await _teamMembershipRepository.IsMemberAsync(
                    request.TeamId.Value,
                    userId,
                    cancellationToken);

            if (!isTeamMember)
            {
                return BadRequest(new
                {
                    message = "The selected team is not available to the current user."
                });
            }
        }

        await using var stream =
            request.File.OpenReadStream();

        var result = await _createDocumentUseCase.ExecuteAsync(
            request.File.FileName,
            request.File.ContentType,
            stream,
            cancellationToken);

        if (request.TeamId.HasValue)
        {
            var shareStatus =
                await _shareDocumentWithTeamUseCase.ExecuteAsync(
                    result.Id,
                    request.TeamId.Value,
                    cancellationToken);

            if (shareStatus is not ShareDocumentStatus.Shared &&
                shareStatus is not ShareDocumentStatus.AlreadyShared)
            {
                return Conflict(new
                {
                    message = "The document was uploaded but could not be shared with the selected team."
                });
            }
        }

        var response = new DocumentResponse(
            result.Id,
            result.FileName,
            result.ContentType,
            result.Status.ToString(),
            true);

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
            result.IsOwner);

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
                document.Status.ToString(),
                document.IsOwner))
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
}
