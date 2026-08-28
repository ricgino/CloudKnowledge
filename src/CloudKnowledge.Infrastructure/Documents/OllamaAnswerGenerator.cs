using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CloudKnowledge.Application.Documents.AskDocuments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class OllamaAnswerGenerator
    : IAnswerGenerator
{
    private const int MaximumAttempts = 2;

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly double _temperature;
    private readonly int _maxTokens;
    private readonly ILogger<OllamaAnswerGenerator> _logger;

    public OllamaAnswerGenerator(
        HttpClient httpClient,
        string model)
        : this(
            httpClient,
            model,
            temperature: 0.1,
            maxTokens: 256,
            NullLogger<OllamaAnswerGenerator>.Instance)
    {
    }

    public OllamaAnswerGenerator(
        HttpClient httpClient,
        string model,
        double temperature,
        int maxTokens,
        ILogger<OllamaAnswerGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(
            httpClient);

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException(
                "Model cannot be empty.",
                nameof(model));
        }

        if (temperature < 0 ||
            temperature > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temperature),
                "Temperature must be between 0 and 2.");
        }

        if (maxTokens < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTokens),
                "Maximum token count must be greater than zero.");
        }

        _httpClient =
            httpClient;

        _model =
            model.Trim();

        _temperature =
            temperature;

        _maxTokens =
            maxTokens;

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));
    }

    public async Task<string> GenerateAsync(
        string question,
        IReadOnlyList<AnswerContextSource> sources,
        CancellationToken cancellationToken)
    {
        var context =
            BuildContext(
                sources);

        var systemPrompt =
            """
            /no_think

            Sei l'assistente di un sistema RAG.

            Devi rispondere esclusivamente usando le fonti
            fornite nel contesto.

            Regole:
            - Non usare conoscenze esterne.
            - Non inventare informazioni mancanti.
            - Se il contesto non contiene abbastanza informazioni,
            dichiaralo chiaramente.
            - Rispondi nella stessa lingua della domanda.
            - Quando affermi qualcosa ricavato dal contesto,
            cita la fonte usando il formato [S1], [S2], ecc.
            - Usa solo identificatori di fonti realmente presenti.
            - Fornisci esclusivamente la risposta finale nel campo "answer".
            - Non mostrare ragionamenti, analisi o passaggi intermedi.
            - Non descrivere come analizzi le fonti.
            - Non ripetere la domanda e non dire cosa sta chiedendo l'utente.
            - Sii conciso ma completo.
            """;

        var baseUserPrompt =
            $"""
            DOMANDA:
            {question}

            FONTI:
            {context}

            Rispondi alla domanda utilizzando esclusivamente
            le fonti sopra riportate.
            """;

        for (var attempt = 1;
             attempt <= MaximumAttempts;
             attempt++)
        {
            var userPrompt =
                attempt == 1
                    ? baseUserPrompt
                    : BuildCorrectivePrompt(
                        baseUserPrompt);

            var answer =
                await GenerateAttemptAsync(
                    systemPrompt,
                    userPrompt,
                    cancellationToken);

            if (IsAcceptableGroundedAnswer(
                    question,
                    answer,
                    sources))
            {
                return answer;
            }

            _logger.LogWarning(
                "Ollama produced a degenerate grounded answer on attempt {Attempt} of {MaximumAttempts}; retrying={Retrying}.",
                attempt,
                MaximumAttempts,
                attempt < MaximumAttempts);
        }

        throw new InvalidOperationException(
            "Ollama did not produce a valid grounded answer after retrying.");
    }

    private async Task<string> GenerateAttemptAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var request =
            new OllamaChatRequest(
                _model,
                new[]
                {
                    new OllamaChatMessage(
                        "system",
                        systemPrompt),

                    new OllamaChatMessage(
                        "user",
                        userPrompt)
                },
                Stream: false,
                Think: false,
                CreateAnswerFormat(),
                new OllamaChatOptions(
                    _temperature,
                    _maxTokens));

        using var response =
            await _httpClient.PostAsJsonAsync(
                "/api/chat",
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content
                .ReadFromJsonAsync<OllamaChatResponse>(
                    cancellationToken);

        var content =
            result?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                "Ollama returned an empty answer.");
        }

        LogTimings(
            result!);

        OllamaStructuredAnswer? structuredAnswer;

        try
        {
            structuredAnswer =
                JsonSerializer.Deserialize<OllamaStructuredAnswer>(
                    content);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Ollama returned an invalid structured answer.",
                exception);
        }

        var answer =
            structuredAnswer?.Answer;

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException(
                "Ollama returned a structured response without an answer.");
        }

        return RemoveThinkingContent(answer);
    }

    private static string BuildCorrectivePrompt(
        string baseUserPrompt)
    {
        return
            $"""
            {baseUserPrompt}

            ATTENZIONE: il tentativo precedente non era una risposta valida.
            Non ripetere la domanda.
            Estrai dalle fonti fatti concreti che rispondono alla domanda.
            Se le fonti contengono informazioni pertinenti, riportale esplicitamente.
            Includi almeno una citazione valida come [S1] nella risposta.
            """;
    }

    private static bool IsAcceptableGroundedAnswer(
        string question,
        string answer,
        IReadOnlyList<AnswerContextSource> sources)
    {
        if (NormalizeComparableText(answer) ==
            NormalizeComparableText(question))
        {
            return false;
        }

        if (sources.Count == 0)
        {
            return true;
        }

        return sources.Any(
            source =>
                answer.Contains(
                    $"[{source.Label}]",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeComparableText(
        string value)
    {
        var builder =
            new StringBuilder();

        var previousWasSeparator =
            false;

        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(
                    char.ToLowerInvariant(character));

                previousWasSeparator =
                    false;

                continue;
            }

            if (!previousWasSeparator &&
                builder.Length > 0)
            {
                builder.Append(' ');
                previousWasSeparator = true;
            }
        }

        return builder
            .ToString()
            .Trim();
    }

    private static OllamaResponseFormat CreateAnswerFormat()
    {
        return new OllamaResponseFormat(
            "object",
            new Dictionary<string, OllamaSchemaProperty>
            {
                ["answer"] =
                    new OllamaSchemaProperty(
                        "string")
            },
            ["answer"],
            AdditionalProperties: false);
    }

    private void LogTimings(
        OllamaChatResponse result)
    {
        _logger.LogInformation(
            "Ollama answer timing model={Model} totalMs={TotalMs} loadMs={LoadMs} promptTokens={PromptTokens} promptMs={PromptMs} outputTokens={OutputTokens} evalMs={EvalMs} doneReason={DoneReason}",
            _model,
            ToMilliseconds(result.TotalDuration),
            ToMilliseconds(result.LoadDuration),
            result.PromptEvalCount,
            ToMilliseconds(result.PromptEvalDuration),
            result.EvalCount,
            ToMilliseconds(result.EvalDuration),
            result.DoneReason);
    }

    private static double? ToMilliseconds(
        long? nanoseconds)
    {
        return nanoseconds.HasValue
            ? nanoseconds.Value / 1_000_000d
            : null;
    }

    private static string RemoveThinkingContent(
        string content)
    {
        var closingTagIndex =
            content.LastIndexOf(
                "</think>",
                StringComparison.OrdinalIgnoreCase);

        if (closingTagIndex >= 0)
        {
            content =
                content[
                    (closingTagIndex + "</think>".Length)..];
        }

        return content.Trim();
    }

    private static string BuildContext(
        IReadOnlyList<AnswerContextSource> sources)
    {
        var builder =
            new StringBuilder();

        foreach (var source in sources)
        {
            builder.AppendLine(
                $"[{source.Label}]");

            builder.AppendLine(
                $"DocumentId: {source.DocumentId}");

            builder.AppendLine(
                $"Chunk position: {source.Position}");

            builder.AppendLine(
                source.Content);

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private sealed record OllamaChatRequest(
        string Model,
        OllamaChatMessage[] Messages,
        bool Stream,
        bool Think,
        OllamaResponseFormat Format,
        OllamaChatOptions Options);

    private sealed record OllamaResponseFormat(
        string Type,
        Dictionary<string, OllamaSchemaProperty> Properties,
        string[] Required,
        [property: JsonPropertyName("additionalProperties")]
        bool AdditionalProperties);

    private sealed record OllamaSchemaProperty(
        string Type);

    private sealed record OllamaChatOptions(
        double Temperature,
        [property: JsonPropertyName("num_predict")]
        int NumPredict);

    private sealed record OllamaChatMessage(
        string Role,
        string Content);

    private sealed record OllamaStructuredAnswer(
        [property: JsonPropertyName("answer")]
        string? Answer);

    private sealed record OllamaChatResponse(
        OllamaChatMessage? Message,
        [property: JsonPropertyName("total_duration")]
        long? TotalDuration,
        [property: JsonPropertyName("load_duration")]
        long? LoadDuration,
        [property: JsonPropertyName("prompt_eval_count")]
        int? PromptEvalCount,
        [property: JsonPropertyName("prompt_eval_duration")]
        long? PromptEvalDuration,
        [property: JsonPropertyName("eval_count")]
        int? EvalCount,
        [property: JsonPropertyName("eval_duration")]
        long? EvalDuration,
        [property: JsonPropertyName("done_reason")]
        string? DoneReason);
}
