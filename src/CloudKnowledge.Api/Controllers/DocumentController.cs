using CloudKnowledge.Api.Contracts.Documents;
using Microsoft.AspNetCore.Mvc;
using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Application.Documents.GetDocument;
using CloudKnowledge.Application.Documents.GetDocuments;

namespace CloudKnowledge.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{

    private readonly CreateDocumentUseCase _createDocumentUseCase;
    private readonly GetDocumentUseCase _getDocumentUseCase;
    private readonly GetDocumentsUseCase _getDocumentsUseCase;

    public DocumentsController(
        CreateDocumentUseCase createDocumentUseCase,
        GetDocumentUseCase getDocumentUseCase,
        GetDocumentsUseCase getDocumentsUseCase)
    {
        _createDocumentUseCase = createDocumentUseCase;
        _getDocumentUseCase = getDocumentUseCase;
        _getDocumentsUseCase = getDocumentsUseCase;
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
    
}