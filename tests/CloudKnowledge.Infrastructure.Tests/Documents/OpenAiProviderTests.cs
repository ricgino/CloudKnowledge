using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Infrastructure.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CloudKnowledge.Infrastructure.Tests.Documents;

public sealed class OpenAiProviderTests
{
    [Fact]
    public void Configuration_ShouldSelectDirectOpenAiModels()
    {
        var configuration = new ConfigurationManager
        {
            ["Ai:Provider"] = "OpenAI",
            ["Ai:Endpoint"] = "https://api.openai.com/",
            ["Ai:ApiKey"] = "test-key",
            ["Ai:EmbeddingModel"] = "text-embedding-3-small",
            ["Ai:AnswerModel"] = "gpt-4.1-nano",
            ["Ai:EmbeddingDimensions"] = "768",
            ["Ai:AnswerTemperature"] = "0.1",
            ["Ai:AnswerMaxTokens"] = "256"
        };

        var result = AiProviderConfiguration.From(
            configuration,
            requireAnswerGenerator: true);

        Assert.Equal("OpenAI", result.Provider);
        Assert.True(result.IsOpenAi);
        Assert.False(result.IsAzureOpenAi);
        Assert.Equal(new Uri("https://api.openai.com/"), result.BaseUrl);
        Assert.Equal("test-key", result.ApiKey);
        Assert.Equal("text-embedding-3-small", result.EmbeddingModel);
        Assert.Equal("gpt-4.1-nano", result.AnswerModel);
        Assert.Equal(768, result.EmbeddingDimensions);
    }

    [Fact]
    public async Task EmbeddingGenerator_ShouldUseDirectV1EndpointAndBearerAuthentication()
    {
        var handler = new RecordingHandler(
            """
            {
              "data": [
                { "index": 0, "embedding": [0.1, 0.2, 0.3] },
                { "index": 1, "embedding": [0.4, 0.5, 0.6] }
              ]
            }
            """);

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/")
        };

        var sut = new OpenAiEmbeddingGenerator(
            httpClient,
            model: "text-embedding-3-small",
            apiKey: "test-key",
            dimensions: 3);

        var result = await sut.GenerateAsync(
            ["first", "second"],
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal([0.1f, 0.2f, 0.3f], result[0]);
        Assert.Equal([0.4f, 0.5f, 0.6f], result[1]);
        Assert.Equal("/v1/embeddings", handler.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("test-key", handler.Authorization?.Parameter);
        Assert.Null(handler.ApiKey);

        using var requestJson =
            JsonDocument.Parse(
                Assert.IsType<string>(handler.RequestBody));

        var root = requestJson.RootElement;
        Assert.Equal(
            "text-embedding-3-small",
            root.GetProperty("model").GetString());
        Assert.Equal(3, root.GetProperty("dimensions").GetInt32());
        Assert.Equal("float", root.GetProperty("encoding_format").GetString());
    }

    [Fact]
    public async Task AnswerGenerator_ShouldUseDirectV1EndpointAndCheapestCompatibleModel()
    {
        var handler = new RecordingHandler(
            """
            {
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "content": "Alperia Smart Services Srl. [S1]"
                  }
                }
              ]
            }
            """);

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/")
        };

        var sut = new OpenAiAnswerGenerator(
            httpClient,
            model: "gpt-4.1-nano",
            apiKey: "test-key",
            temperature: 0.1,
            maxTokens: 256);

        var result = await sut.GenerateAsync(
            "Qual è l'azienda?",
            [
                new AnswerContextSource(
                    "S1",
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    0,
                    "Alperia Smart Services Srl.")
            ],
            CancellationToken.None);

        Assert.Equal("Alperia Smart Services Srl. [S1]", result);
        Assert.Equal(
            "/v1/chat/completions",
            handler.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("test-key", handler.Authorization?.Parameter);
        Assert.Null(handler.ApiKey);

        using var requestJson =
            JsonDocument.Parse(
                Assert.IsType<string>(handler.RequestBody));

        var root = requestJson.RootElement;
        var messages = root.GetProperty("messages");
        var userMessage = messages[1].GetProperty("content").GetString();

        Assert.Equal("gpt-4.1-nano", root.GetProperty("model").GetString());
        Assert.NotNull(userMessage);
        Assert.Contains("Qual è l'azienda?", userMessage);
        Assert.Contains("[S1]", userMessage);
        Assert.Equal(256, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.1, root.GetProperty("temperature").GetDouble(), 3);
    }

    [Fact]
    public async Task RetrievalQueryGenerator_ShouldRewriteComplexQuestionIntoTechnicalQueries()
    {
        const string question =
            "Posso installare un ACS880-01 a 3500 metri di altitudine mantenendo la corrente nominale completa?";

        var handler = new RecordingHandler(
            """
            {
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "content": "{\"queries\":[\"ACS880-01 installation altitude 3500 m\",\"ACS880-01 altitude derating output current\"]}"
                  }
                }
              ]
            }
            """);

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/")
        };

        var configuration =
            new AiProviderConfiguration(
                AiProviderConfiguration.OpenAiProvider,
                new Uri("https://api.openai.com/"),
                "test-key",
                "text-embedding-3-small",
                "gpt-4.1-nano",
                768,
                0.1,
                256);

        var sut =
            new AiRetrievalQueryGenerator(
                httpClient,
                configuration,
                NullLogger<AiRetrievalQueryGenerator>.Instance);

        var result =
            await sut.GenerateAsync(
                question,
                maximumQueries: 3,
                CancellationToken.None);

        Assert.Contains(
            result,
            query =>
                query.Contains(
                    "derating",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            result,
            query =>
                query.Contains(
                    "ACS880-01",
                    StringComparison.OrdinalIgnoreCase)
                && query.Contains(
                    "3500",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Equal(
            "/v1/chat/completions",
            handler.RequestUri?.AbsolutePath);

        Assert.Equal(
            "Bearer",
            handler.Authorization?.Scheme);

        using var requestJson =
            JsonDocument.Parse(
                Assert.IsType<string>(handler.RequestBody));

        var root = requestJson.RootElement;
        var messages = root.GetProperty("messages");
        var systemMessage = messages[0].GetProperty("content").GetString();
        var userMessage = messages[1].GetProperty("content").GetString();

        Assert.NotNull(systemMessage);
        Assert.Contains(
            "operating range",
            systemMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "rated performance",
            systemMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "derating",
            systemMessage,
            StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(userMessage);
        Assert.Contains(question, userMessage);
        Assert.Equal(
            "json_object",
            root.GetProperty("response_format")
                .GetProperty("type")
                .GetString());
    }

    private sealed class RecordingHandler(string responseJson)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public string? ApiKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization;

            if (request.Headers.TryGetValues("api-key", out var values))
            {
                ApiKey = values.SingleOrDefault();
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseJson,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
