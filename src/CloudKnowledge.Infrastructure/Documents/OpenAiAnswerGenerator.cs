using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using CloudKnowledge.Application.Documents.AskDocuments;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class OpenAiAnswerGenerator
    : IAnswerGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly double _temperature;
    private readonly int _maxTokens;

    public OpenAiAnswerGenerator(
        HttpClient httpClient,
        string model,
        string apiKey,
        double temperature,
        int maxTokens)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException(
                "Model cannot be empty.",
                nameof(model));
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
        _model = model.Trim();
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
            new OpenAiChatRequest(
                _model,
                [
                    new OpenAiChatMessage(
                        "system",
                        BuildSystemPrompt()),
                    new OpenAiChatMessage(
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
                "/v1/chat/completions")
            {
                Content = JsonContent.Create(requestBody)
            };

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _apiKey);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content
                .ReadFromJsonAsync<OpenAiChatResponse>(
                    cancellationToken);

        var answer =
            result?.Choices?
                .FirstOrDefault()?
                .Message?
                .Content;

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException(
                "OpenAI returned an empty answer.");
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
            L'assenza di una restrizione o di un limite nelle fonti recuperate non è una prova che tale restrizione o limite non esista.
            Non concludere che un'operazione sia consentita, sicura o possibile senza limitazioni se le fonti non lo affermano esplicitamente.
            Ogni ruolo, categoria o relazione attribuita a un'entità deve essere supportata dalle fonti recuperate; non completare collegamenti mancanti per supposizione.
            La co-occorrenza di due nomi, fatti o attributi nello stesso documento o contesto non dimostra da sola una relazione tra loro.
            Se la domanda contiene un qualificatore o una categoria, verifica sia l'appartenenza dell'entità a quella categoria sia la relazione richiesta prima di includerla nella risposta.
            Quando una fonte esplicita un'associazione tra due elementi, preserva entrambi gli estremi della relazione nella risposta; non ridurre un'associazione esplicita a un elenco di soli valori se il contesto fornisce anche le entità corrispondenti.
            Se le fonti supportano solo una parte della domanda, dichiara quale relazione o qualificatore non può essere verificato e rispondi comunque alla parte supportata senza inventare il collegamento mancante.
            Se il qualificatore non può essere verificato ma una relazione più generale è supportata, dichiara il gap e fornisci comunque le associazioni supportate.
            Se fonti diverse forniscono condizioni complementari rilevanti, combinale prima di concludere.
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
        var builder = new StringBuilder();

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

    private sealed record OpenAiChatRequest(
        [property: JsonPropertyName("model")]
        string Model,
        [property: JsonPropertyName("messages")]
        OpenAiChatMessage[] Messages,
        [property: JsonPropertyName("temperature")]
        double Temperature,
        [property: JsonPropertyName("max_tokens")]
        int MaxTokens);

    private sealed record OpenAiChatMessage(
        [property: JsonPropertyName("role")]
        string Role,
        [property: JsonPropertyName("content")]
        string Content);

    private sealed record OpenAiChatResponse(
        [property: JsonPropertyName("choices")]
        OpenAiChatChoice[] Choices);

    private sealed record OpenAiChatChoice(
        [property: JsonPropertyName("message")]
        OpenAiChatMessage Message);
}
