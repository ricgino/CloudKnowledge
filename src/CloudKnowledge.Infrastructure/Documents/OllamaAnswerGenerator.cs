using System.Net.Http.Json;
using System.Text;
using CloudKnowledge.Application.Documents.AskDocuments;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class OllamaAnswerGenerator
    : IAnswerGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaAnswerGenerator(
        HttpClient httpClient,
        string model)
    {
        _httpClient =
            httpClient;

        _model =
            model;
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
            - Fornisci esclusivamente la risposta finale.
            - Non mostrare ragionamenti, analisi o passaggi intermedi.
            - Sii conciso ma completo.
            """;

        var userPrompt =
            $"""
            DOMANDA:
            {question}

            FONTI:
            {context}

            Rispondi alla domanda utilizzando esclusivamente
            le fonti sopra riportate.
            """;

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
                Think: false);

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

        var answer =
            result?.Message?.Content;

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException(
                "Ollama returned an empty answer.");
        }

        return RemoveThinkingContent(answer);
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
        bool Think);

    private sealed record OllamaChatMessage(
        string Role,
        string Content);

    private sealed record OllamaChatResponse(
        OllamaChatMessage? Message);
}