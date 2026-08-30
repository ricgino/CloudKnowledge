namespace CloudKnowledge.Application.Documents.AskDocuments;

public interface IRetrievalQueryGenerator
{
    Task<IReadOnlyList<string>> GenerateAsync(
        string question,
        int maximumQueries,
        CancellationToken cancellationToken);
}
