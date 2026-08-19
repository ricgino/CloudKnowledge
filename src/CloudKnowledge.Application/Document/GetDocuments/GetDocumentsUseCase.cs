using CloudKnowledge.Application.Documents;

namespace CloudKnowledge.Application.Documents.GetDocuments;

public sealed class GetDocumentsUseCase
{
    private const int MaxPageSize = 100;

    private readonly IDocumentRepository _documentRepository;

    public GetDocumentsUseCase(
        IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<GetDocumentsResult> ExecuteAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                "Page must be greater than zero.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                $"Page size must be between 1 and {MaxPageSize}.");
        }

        var skip = (page - 1) * pageSize;

        var totalCount = await _documentRepository.CountAsync(
            cancellationToken);

        var documents = await _documentRepository.GetPageAsync(
            skip,
            pageSize,
            cancellationToken);

        var items = documents
            .Select(document => new GetDocumentsItem(
                document.Id,
                document.FileName,
                document.ContentType,
                document.Status))
            .ToList();

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pageSize);

        return new GetDocumentsResult(
            items,
            page,
            pageSize,
            totalCount,
            totalPages);
    }
}