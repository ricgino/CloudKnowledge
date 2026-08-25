namespace CloudKnowledge.Application.Documents.AskDocuments;

public interface IAnswerGenerator
{
    Task<string> GenerateAsync(
        string question,
        IReadOnlyList<AnswerContextSource> sources,
        CancellationToken cancellationToken);
}