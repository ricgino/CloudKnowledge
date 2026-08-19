using CloudKnowledge.Api.Contracts.Documents;
using Microsoft.AspNetCore.Mvc;
using CloudKnowledge.Application.Documents.CreateDocument;

namespace CloudKnowledge.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{

    private readonly CreateDocumentUseCase _createDocumentUseCase;

    public DocumentsController(
        CreateDocumentUseCase createDocumentUseCase)
    {
        _createDocumentUseCase = createDocumentUseCase;
    }

    [HttpPost]
    public ActionResult<CreateDocumentResponse> Create(
        CreateDocumentRequest request)
    {
        var result = _createDocumentUseCase.Execute(
            request.FileName,
            request.ContentType);

        var response = new CreateDocumentResponse(
            result.Id,
            result.FileName,
            result.ContentType,
            result.Status.ToString());

        return StatusCode(StatusCodes.Status201Created, response);
    }
}