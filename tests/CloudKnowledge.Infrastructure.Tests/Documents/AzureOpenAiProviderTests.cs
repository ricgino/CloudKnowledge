using System.Net;
using System.Text;
using System.Text.Json;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Infrastructure.Documents;
using Microsoft.Extensions.Configuration;

namespace CloudKnowledge.Infrastructure.Tests.Documents;

public sealed class AzureOpenAiProviderTests
{
    [Fact]
    public void Configuration_ShouldSelectAzureOpenAiWithoutDatedApiVersion()
    {
        var configuration = new ConfigurationManager
        {
            ["Ai:Provider"] = "AzureOpenAI",
            ["Ai:Endpoint"] = "https://cloudknowledge.openai.azure.com/",
            ["Ai:ApiKey"] = "test-key",
            ["Ai:EmbeddingDeployment"] = "embedding-small",
            ["Ai:AnswerDeployment"] = "answer-small",
            ["Ai:EmbeddingDimensions"] = "768",
            ["Ai:AnswerTemperature"] = "0.1",
            ["Ai:AnswerMaxTokens"] = "256"
        };

        var result = AiProviderConfiguration.From(
            configuration,
            requireAnswerGenerator: true);

        Assert.Equal("AzureOpenAI", result.Provider);
        Assert.Equal(768, result.EmbeddingDimensions);
        Assert.Equal("embedding-small", result.EmbeddingModel);
        Assert.Equal("answer-small", result.AnswerModel);
    }

    [Fact]
    public async Task EmbeddingGenerator_ShouldUseV1EndpointAndDeploymentAsModel()
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
            BaseAddress = new Uri("https://cloudknowledge.openai.azure.com/")
        };

        var sut = new AzureOpenAiEmbeddingGenerator(
            httpClient,
            deployment: "embedding-small",
            apiKey: "test-key",
            dimensions: 3);

        var result = await sut.GenerateAsync(
            ["first", "second"],
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal([0.1f, 0.2f, 0.3f], result[0]);
        Assert.Equal([0.4f, 0.5f, 0.6f], result[1]);
        Assert.Equal(
            "/openai/v1/embeddings",
            handler.RequestUri?.AbsolutePath);
        Assert.Empty(handler.RequestUri?.Query ?? string.Empty);
        Assert.Equal("test-key", handler.ApiKey);

        using var requestJson =
            JsonDocument.Parse(
                Assert.IsType<string>(handler.RequestBody));

        var root = requestJson.RootElement;
        Assert.Equal("embedding-small", root.GetProperty("model").GetString());
        Assert.Equal(3, root.GetProperty("dimensions").GetInt32());
    }

    [Fact]
    public async Task AnswerGenerator_ShouldUseV1EndpointAndDeploymentAsModel()
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
            BaseAddress = new Uri("https://cloudknowledge.openai.azure.com/")
        };

        var sut = new AzureOpenAiAnswerGenerator(
            httpClient,
            deployment: "answer-small",
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
            "/openai/v1/chat/completions",
            handler.RequestUri?.AbsolutePath);
        Assert.Empty(handler.RequestUri?.Query ?? string.Empty);
        Assert.Equal("test-key", handler.ApiKey);

        using var requestJson =
            JsonDocument.Parse(
                Assert.IsType<string>(handler.RequestBody));

        var root = requestJson.RootElement;
        var messages = root.GetProperty("messages");
        var userMessage = messages[1].GetProperty("content").GetString();

        Assert.Equal("answer-small", root.GetProperty("model").GetString());
        Assert.NotNull(userMessage);
        Assert.Contains("Qual è l'azienda?", userMessage);
        Assert.Contains("[S1]", userMessage);
        Assert.Equal(256, root.GetProperty("max_tokens").GetInt32());
    }

    private sealed class RecordingHandler(string responseJson)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public string? ApiKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

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
