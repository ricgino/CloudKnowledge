using System.Net.Http.Json;
using CloudKnowledge.Application.Documents;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class OllamaEmbeddingGenerator
    : IEmbeddingGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _inputPrefix;

    public int Dimensions { get; }

    public OllamaEmbeddingGenerator(
        HttpClient httpClient,
        string model,
        string inputPrefix,
        int dimensions)
    {
        _httpClient = httpClient;
        _model = model;
        _inputPrefix = inputPrefix;
        Dimensions = dimensions;
    }

    public async Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        var prefixedInputs =
            inputs
                .Select(
                    input =>
                        _inputPrefix + input)
                .ToArray();

        var request =
            new OllamaEmbeddingRequest(
                _model,
                prefixedInputs);

        using var response =
            await _httpClient.PostAsJsonAsync(
                "/api/embed",
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content
                .ReadFromJsonAsync<OllamaEmbeddingResponse>(
                    cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException(
                "Ollama returned an empty response.");
        }

        if (result.Embeddings.Length != inputs.Count)
        {
            throw new InvalidOperationException(
                "Ollama returned an unexpected number of embeddings.");
        }

        foreach (var embedding in result.Embeddings)
        {
            if (embedding.Length != Dimensions)
            {
                throw new InvalidOperationException(
                    $"Expected {Dimensions} dimensions " +
                    $"but received {embedding.Length}.");
            }
        }

        return result.Embeddings;
    }

    private sealed record OllamaEmbeddingRequest(
        string Model,
        string[] Input);

    private sealed record OllamaEmbeddingResponse(
        float[][] Embeddings);
}