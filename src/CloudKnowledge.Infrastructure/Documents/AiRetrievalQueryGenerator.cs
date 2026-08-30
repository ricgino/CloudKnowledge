using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CloudKnowledge.Application.Documents.AskDocuments;
using Microsoft.Extensions.Logging;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class AiRetrievalQueryGenerator
    : IRetrievalQueryGenerator
{
    private const int MaximumPlanningTokens = 192;

    private readonly HttpClient _httpClient;
    private readonly AiProviderConfiguration _configuration;
    private readonly ILogger<AiRetrievalQueryGenerator> _logger;

    public AiRetrievalQueryGenerator(
        HttpClient httpClient,
        AiProviderConfiguration configuration,
        ILogger<AiRetrievalQueryGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(configuration.AnswerModel))
        {
            throw new ArgumentException(
                "An answer model is required for retrieval query generation.",
                nameof(configuration));
        }

        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GenerateAsync(
        string question,
        int maximumQueries,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException(
                "Question cannot be empty.",
                nameof(question));
        }

        if (maximumQueries < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumQueries));
        }

        try
        {
            var content =
                _configuration.Provider switch
                {
                    AiProviderConfiguration.OpenAiProvider =>
                        await GenerateOpenAiCompatibleAsync(
                            question,
                            maximumQueries,
                            useAzureApiKeyHeader: false,
                            cancellationToken),

                    AiProviderConfiguration.AzureOpenAiProvider =>
                        await GenerateOpenAiCompatibleAsync(
                            question,
                            maximumQueries,
                            useAzureApiKeyHeader: true,
                            cancellationToken),

                    AiProviderConfiguration.OllamaProvider =>
                        await GenerateOllamaAsync(
                            question,
                            maximumQueries,
                            cancellationToken),

                    _ =>
                        throw new InvalidOperationException(
                            $"Unsupported AI provider '{_configuration.Provider}'.")
                };

            var queries =
                ParseQueries(
                    content,
                    maximumQueries);

            if (queries.Count > 0)
            {
                _logger.LogInformation(
                    "Generated retrieval queries: {Queries}",
                    string.Join(" | ", queries));

                return queries;
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is HttpRequestException
                  or JsonException
                  or InvalidOperationException
                  or TaskCanceledException)
        {
            _logger.LogWarning(
                exception,
                "AI retrieval query generation failed; using heuristic fallback.");
        }

        return RetrievalQueryPlanner.CreateFocusedQueries(
            question,
            maximumQueries);
    }

    private async Task<string> GenerateOpenAiCompatibleAsync(
        string question,
        int maximumQueries,
        bool useAzureApiKeyHeader,
        CancellationToken cancellationToken)
    {
        var requestBody =
            new
            {
                model = _configuration.AnswerModel!,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = BuildSystemPrompt()
                    },
                    new
                    {
                        role = "user",
                        content = BuildUserPrompt(
                            question,
                            maximumQueries)
                    }
                },
                temperature = 0,
                max_tokens = MaximumPlanningTokens,
                response_format = new
                {
                    type = "json_object"
                }
            };

        var requestPath =
            useAzureApiKeyHeader
                ? "/openai/v1/chat/completions"
                : "/v1/chat/completions";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                requestPath)
            {
                Content = JsonContent.Create(
                    requestBody)
            };

        if (useAzureApiKeyHeader)
        {
            request.Headers.TryAddWithoutValidation(
                "api-key",
                _configuration.ApiKey!);
        }
        else
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _configuration.ApiKey!);
        }

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseText =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        using var responseJson =
            JsonDocument.Parse(
                responseText);

        var choices =
            responseJson.RootElement.GetProperty(
                "choices");

        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "AI provider returned no retrieval-query choices.");
        }

        var content =
            choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                "AI provider returned empty retrieval-query content.");
        }

        return content;
    }

    private async Task<string> GenerateOllamaAsync(
        string question,
        int maximumQueries,
        CancellationToken cancellationToken)
    {
        var requestBody =
            new
            {
                model = _configuration.AnswerModel!,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = BuildSystemPrompt()
                    },
                    new
                    {
                        role = "user",
                        content = BuildUserPrompt(
                            question,
                            maximumQueries)
                    }
                },
                stream = false,
                think = false,
                format = new
                {
                    type = "object",
                    properties = new
                    {
                        queries = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "string"
                            }
                        }
                    },
                    required = new[]
                    {
                        "queries"
                    },
                    additionalProperties = false
                },
                options = new
                {
                    temperature = 0,
                    num_predict = MaximumPlanningTokens
                }
            };

        using var response =
            await _httpClient.PostAsJsonAsync(
                "/api/chat",
                requestBody,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseText =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        using var responseJson =
            JsonDocument.Parse(
                responseText);

        var content =
            responseJson.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                "Ollama returned empty retrieval-query content.");
        }

        return content;
    }

    private static IReadOnlyList<string> ParseQueries(
        string content,
        int maximumQueries)
    {
        using var json =
            JsonDocument.Parse(
                content);

        if (!json.RootElement.TryGetProperty(
                "queries",
                out var queriesElement) ||
            queriesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "AI retrieval-query response did not contain a queries array.");
        }

        return queriesElement
            .EnumerateArray()
            .Where(
                item =>
                    item.ValueKind == JsonValueKind.String)
            .Select(
                item =>
                    item.GetString()?.Trim())
            .Where(
                query =>
                    !string.IsNullOrWhiteSpace(query))
            .Select(
                query =>
                    query!)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Take(maximumQueries)
            .ToArray();
    }

    private static string BuildSystemPrompt()
    {
        return
            """
            You generate semantic retrieval queries for a private document knowledge system.

            Your job is retrieval planning, not answering the user's question.

            Rules:
            - Return only a JSON object with a "queries" array of strings.
            - Produce concise complementary search queries, not long paraphrases.
            - Preserve exact product codes, error codes, numbers and units from the question.
            - Decompose independent constraints or subquestions when useful.
            - For feasibility questions that combine an operating condition with maintaining rated or nominal performance, dedicate separate queries to the operating range and to rated performance derating, reduction or limitations under that condition.
            - Name the affected performance quantity explicitly in the limitation query when the user names one, for example current, power, speed, load or capacity.
            - Prefer coverage of independent answer-critical constraints over multiple near-duplicate environmental or installation queries.
            - Use likely terminology and standard synonyms that may appear in technical source documents.
            - When the user writes in a language other than English, include concise English technical wording when useful because the source documents may be in English.
            - You may introduce standard terminology needed for retrieval, but never invent factual thresholds, limits, dates, values or answers that were not stated by the user.
            - Do not include explanations, markdown, numbering or an answer.
            """;
    }

    private static string BuildUserPrompt(
        string question,
        int maximumQueries)
    {
        return
            $"""
            Maximum queries: {maximumQueries}

            USER QUESTION:
            {question}
            """;
    }
}
