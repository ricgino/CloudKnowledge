using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Users;
using CloudKnowledge.Domain.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace CloudKnowledge.Api.Controllers;

[Authorize]
[RequiredScope("access_as_user")]
[ApiController]
[Route("api/documents")]
public sealed class DocumentRetryController : ControllerBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentProcessingQueue _documentProcessingQueue;
    private readonly ICurrentUser _currentUser;

    public DocumentRetryController(
        IDocumentRepository documentRepository,
        IDocumentProcessingQueue documentProcessingQueue,
        ICurrentUser currentUser)
    {
        _documentRepository =
            documentRepository;

        _documentProcessingQueue =
            documentProcessingQueue;

        _currentUser =
            currentUser;
    }

    [HttpPost("{documentId:guid}/retry")]
    public async Task<IActionResult> Retry(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var userId =
            await _currentUser.GetUserIdAsync(
                cancellationToken);

        var document =
            await _documentRepository.GetByIdAsync(
                documentId,
                cancellationToken);

        if (document is null ||
            document.OwnerUserId != userId ||
            document.Status != DocumentStatus.Failed)
        {
            return NotFound();
        }

        document.RetryProcessing();

        await _documentRepository.UpdateAsync(
            document,
            cancellationToken);

        await _documentProcessingQueue.PublishAsync(
            document.Id,
            cancellationToken);

        return NoContent();
    }
}
