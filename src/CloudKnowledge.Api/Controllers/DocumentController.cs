using CloudKnowledge.Api.Contracts.Documents;
using Microsoft.AspNetCore.Mvc;
using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Application.Documents.GetDocument;

namespace CloudKnowledge.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{

    private readonly CreateDocumentUseCase _createDocumentUseCase;
    private readonly GetDocumentUseCase _getDocumentUseCase;

    public DocumentsController(
        CreateDocumentUseCase createDocumentUseCase,
        GetDocumentUseCase getDocumentUseCase)
    {
        _createDocumentUseCase = createDocumentUseCase;
        _getDocumentUseCase = getDocumentUseCase;
    }

    [HttpPost]
    public async Task<ActionResult<DocumentResponse>> Create(
        CreateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createDocumentUseCase.ExecuteAsync(
            request.FileName,
            request.ContentType,
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
    
}