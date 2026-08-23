namespace CloudKnowledge.Application.Documents;

public interface IEmbeddingGenerator
{
    int Dimensions { get; }

    Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken);
}