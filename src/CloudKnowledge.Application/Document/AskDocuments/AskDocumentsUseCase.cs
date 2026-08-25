using CloudKnowledge.Application.Documents.SearchDocuments;

namespace CloudKnowledge.Application.Documents.AskDocuments;

public sealed class AskDocumentsUseCase
{
    private readonly SearchDocumentsUseCase
        _searchDocumentsUseCase;

    private readonly IAnswerGenerator
        _answerGenerator;

    public AskDocumentsUseCase(
        SearchDocumentsUseCase searchDocumentsUseCase,
        IAnswerGenerator answerGenerator)
    {
        _searchDocumentsUseCase =
            searchDocumentsUseCase;

        _answerGenerator =
            answerGenerator;
    }

    public async Task<AskDocumentsResult> ExecuteAsync(
        string question,
        int take,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException(
                "Question cannot be empty.",
                nameof(question));
        }

        if (take < 1 || take > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                "Take must be between 1 and 10.");
        }

        var searchResults =
            await _searchDocumentsUseCase.ExecuteAsync(
                question,
                take,
                cancellationToken);

        if (searchResults.Count == 0)
        {
            return new AskDocumentsResult(
                "Non sono state trovate informazioni pertinenti nei documenti.",
                Array.Empty<AskDocumentsSource>());
        }

        var contextSources =
            searchResults
                .Select(
                    (result, index) =>
                        new AnswerContextSource(
                            $"S{index + 1}",
                            result.DocumentId,
                            result.ChunkId,
                            result.Position,
                            result.Content))
                .ToArray();

        var answer =
            await _answerGenerator.GenerateAsync(
                question,
                contextSources,
                cancellationToken);

        var sources =
            searchResults
                .Select(
                    (result, index) =>
                        new AskDocumentsSource(
                            $"S{index + 1}",
                            result.DocumentId,
                            result.ChunkId,
                            result.Position,
                            result.Content,
                            1.0 - result.CosineDistance))
                .ToArray();

        return new AskDocumentsResult(
            answer,
            sources);
    }
}