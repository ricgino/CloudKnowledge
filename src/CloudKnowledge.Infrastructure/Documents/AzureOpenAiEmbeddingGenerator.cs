using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CloudKnowledge.Application.Documents;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class AzureOpenAiEmbeddingGenerator
    : IEmbeddingGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _deployment;
    private readonly string _apiKey;
    private readonly string _apiVersion;

    public int Dimensions { get; }

    public AzureOpenAiEmbeddingGenerator(
        HttpClient httpClient,
        string deployment,
        string apiKey,
        string apiVersion,
        int dimensions)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (string.IsNullOrWhiteSpace(deployment))
        {
            throw new ArgumentException(
                "Deployment cannot be empty.",
                nameof(deployment));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException(
                "API key cannot be empty.",
                nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(apiVersion))
        {
            throw new ArgumentException(
                "API version cannot be empty.",
                nameof(apiVersion));
        }

        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimensions));
        }

        _httpClient = httpClient;
        _deployment = deployment.Trim();
        _apiKey = apiKey.Trim();
        _apiVersion = apiVersion.Trim();
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

        var requestBody =
            new AzureOpenAiEmbeddingRequest(
                inputs.ToArray(),
                Dimensions,
                EncodingFormat: "float");

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                BuildRequestPath())
            {
                Content =
                    JsonContent.Create(
                        requestBody)
            };

        request.Headers.TryAddWithoutValidation(
            "api-key",
            _apiKey);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content
                .ReadFromJsonAsync<AzureOpenAiEmbeddingResponse>(
                    cancellationToken);

        if (result?.Data is null ||
            result.Data.Length != inputs.Count)
        {
            throw new InvalidOperationException(
                "Azure OpenAI returned an unexpected number of embeddings.");
        }

        var embeddings =
            result.Data
                .OrderBy(item => item.Index)
                .Select(item => item.Embedding)
                .ToArray();

        foreach (var embedding in embeddings)
        {
            if (embedding is null ||
                embedding.Length != Dimensions)
            {
                throw new InvalidOperationException(
                    $"Expected {Dimensions} dimensions " +
                    $"but received {embedding?.Length ?? 0}.");
            }
        }

        return embeddings!;
    }

    private string BuildRequestPath()
    {
        return
            $"/openai/deployments/{Uri.EscapeDataString(_deployment)}/embeddings" +
            $"?api-version={Uri.EscapeDataString(_apiVersion)}";
    }

    private sealed record AzureOpenAiEmbeddingRequest(
        [property: JsonPropertyName("input")]
        string[] Input,
        [property: JsonPropertyName("dimensions")]
        int Dimensions,
        [property: JsonPropertyName("encoding_format")]
        string EncodingFormat);

    private sealed record AzureOpenAiEmbeddingResponse(
        [property: JsonPropertyName("data")]
        AzureOpenAiEmbeddingItem[] Data);

    private sealed record AzureOpenAiEmbeddingItem(
        [property: JsonPropertyName("index")]
        int Index,
        [property: JsonPropertyName("embedding")]
        float[] Embedding);
}
