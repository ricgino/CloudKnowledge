using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using CloudKnowledge.Application.Documents.AskDocuments;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class AzureOpenAiAnswerGenerator
    : IAnswerGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _deployment;
    private readonly string _apiKey;
    private readonly double _temperature;
    private readonly int _maxTokens;

    public AzureOpenAiAnswerGenerator(
        HttpClient httpClient,
        string deployment,
        string apiKey,
        double temperature,
        int maxTokens)
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

        if (temperature < 0 || temperature > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temperature));
        }

        if (maxTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTokens));
        }

        _httpClient = httpClient;
        _deployment = deployment.Trim();
        _apiKey = apiKey.Trim();
        _temperature = temperature;
        _maxTokens = maxTokens;
    }

    public async Task<string> GenerateAsync(
        string question,
        IReadOnlyList<AnswerContextSource> sources,
        CancellationToken cancellationToken)
    {
        var requestBody =
            new AzureOpenAiChatRequest(
                _deployment,
                [
                    new AzureOpenAiChatMessage(
                        "system",
                        BuildSystemPrompt()),
                    new AzureOpenAiChatMessage(
                        "user",
                        BuildUserPrompt(
                            question,
                            sources))
                ],
                _temperature,
                _maxTokens);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/openai/v1/chat/completions")
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
                .ReadFromJsonAsync<AzureOpenAiChatResponse>(
                    cancellationToken);

        var answer =
            result?.Choices?
                .FirstOrDefault()?
                .Message?
                .Content;

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException(
                "Azure OpenAI returned an empty answer.");
        }

        return answer.Trim();
    }

    private static string BuildSystemPrompt()
    {
        return
            """
            Sei l'assistente di un sistema RAG.

            Rispondi esclusivamente usando le fonti fornite nel contesto.
            Non usare conoscenze esterne e non inventare informazioni mancanti.
            Se il contesto non contiene abbastanza informazioni, dichiaralo chiaramente.
            Rispondi nella stessa lingua della domanda.
            Quando usi una fonte, cita il suo identificatore [S1], [S2], ecc.
            Usa solo identificatori realmente presenti nel contesto.
            Non mostrare ragionamenti o passaggi intermedi.
            Sii conciso ma completo.
            """;
    }

    private static string BuildUserPrompt(
        string question,
        IReadOnlyList<AnswerContextSource> sources)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine("DOMANDA:");
        builder.AppendLine(question);
        builder.AppendLine();
        builder.AppendLine("FONTI:");

        foreach (var source in sources)
        {
            builder.AppendLine($"[{source.Label}]");
            builder.AppendLine($"DocumentId: {source.DocumentId}");
            builder.AppendLine($"Chunk position: {source.Position}");
            builder.AppendLine(source.Content);
            builder.AppendLine();
        }

        builder.AppendLine(
            "Rispondi alla domanda utilizzando esclusivamente le fonti sopra riportate.");

        return builder.ToString();
    }

    private sealed record AzureOpenAiChatRequest(
        [property: JsonPropertyName("model")]
        string Model,
        [property: JsonPropertyName("messages")]
        AzureOpenAiChatMessage[] Messages,
        [property: JsonPropertyName("temperature")]
        double Temperature,
        [property: JsonPropertyName("max_tokens")]
        int MaxTokens);

    private sealed record AzureOpenAiChatMessage(
        [property: JsonPropertyName("role")]
        string Role,
        [property: JsonPropertyName("content")]
        string Content);

    private sealed record AzureOpenAiChatResponse(
        [property: JsonPropertyName("choices")]
        AzureOpenAiChatChoice[] Choices);

    private sealed record AzureOpenAiChatChoice(
        [property: JsonPropertyName("message")]
        AzureOpenAiChatMessage Message);
}
