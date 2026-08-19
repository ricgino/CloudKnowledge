using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Application.Documents.CreateDocument;

public sealed class CreateDocumentUseCase
{
    public CreateDocumentResult Execute(
        string fileName,
        string contentType)
    {
        var document = Document.Create(
            fileName,
            contentType);

        return new CreateDocumentResult(
            document.Id,
            document.FileName,
            document.ContentType,
            document.Status);
    }
}