using System.Net;
using System.Text;
using System.Text.Json;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Infrastructure.Documents;

namespace CloudKnowledge.Infrastructure.Tests.Documents;

public sealed class RelationshipGroundingPromptTests
{
    [Fact]
    public async Task OpenAiPrompt_ShouldRequireSupportedRelationsAndAllowPartialAnswer()
    {
        var handler =
            new RecordingHandler(
                CreateOpenAiResponse());

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.openai.com/")
            };

        var sut =
            new OpenAiAnswerGenerator(
                httpClient,
                model: "gpt-4.1-nano",
                apiKey: "test-key",
                temperature: 0.1,
                maxTokens: 256);

        await AskRelationshipQuestionAsync(
            sut,
            CancellationToken.None);

        AssertRelationshipRules(
            ReadSystemPrompt(handler.RequestBody));
    }

    [Fact]
    public async Task AzureOpenAiPrompt_ShouldRequireSupportedRelationsAndAllowPartialAnswer()
    {
        var handler =
            new RecordingHandler(
                CreateOpenAiResponse());

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://example.openai.azure.com/")
            };

        var sut =
            new AzureOpenAiAnswerGenerator(
                httpClient,
                deployment: "gpt-4.1-nano",
                apiKey: "test-key",
                temperature: 0.1,
                maxTokens: 256);

        await AskRelationshipQuestionAsync(
            sut,
            CancellationToken.None);

        AssertRelationshipRules(
            ReadSystemPrompt(handler.RequestBody));
    }

    [Fact]
    public async Task OllamaPrompt_ShouldRequireSupportedRelationsAndAllowPartialAnswer()
    {
        var handler =
            new RecordingHandler(
                """
                {
                  "message": {
                    "role": "assistant",
                    "content": "{\"answer\":\"Le fonti non permettono di verificare quali personaggi siano cani protagonisti. [S1]\"}"
                  },
                  "done_reason": "stop"
                }
                """);

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:11434")
            };

        var sut =
            new OllamaAnswerGenerator(
                httpClient,
                "qwen3:4b");

        await AskRelationshipQuestionAsync(
            sut,
            CancellationToken.None);

        AssertRelationshipRules(
            ReadSystemPrompt(handler.RequestBody));
    }

    private static async Task AskRelationshipQuestionAsync(
        IAnswerGenerator generator,
        CancellationToken cancellationToken)
    {
        await generator.GenerateAsync(
            "Quali attori doppiano i cani protagonisti?",
            [
                new AnswerContextSource(
                    "S1",
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    0,
                    "Principal Cast: MAYOR KOBAYASHI Kunichi Nomura.")
            ],
            cancellationToken);
    }

    private static void AssertRelationshipRules(
        string? systemPrompt)
    {
        Assert.NotNull(systemPrompt);
        Assert.Contains(
            "relazione",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "categoria",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "co-occorrenza",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "parte della domanda",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadSystemPrompt(
        string? requestBody)
    {
        using var requestJson =
            JsonDocument.Parse(
                Assert.IsType<string>(requestBody));

        return requestJson.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString();
    }

    private static string CreateOpenAiResponse()
    {
        return
            """
            {
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "content": "Le fonti non permettono di verificare quali personaggi siano cani protagonisti. [S1]"
                  }
                }
              ]
            }
            """;
    }

    private sealed class RecordingHandler(string responseJson)
        : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody =
                request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(
                        cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content =
                    new StringContent(
                        responseJson,
                        Encoding.UTF8,
                        "application/json")
            };
        }
    }
}
