using System.Net;
using System.Text;
using System.Text.Json;
using CloudKnowledge.Infrastructure.Documents;
using Microsoft.Extensions.Logging.Abstractions;

namespace CloudKnowledge.Infrastructure.Tests.Documents;

public sealed class RelationshipRetrievalPlanningPromptTests
{
    [Fact]
    public async Task RetrievalPlanner_ShouldSeparateEntityRoleMappingFromCategoryMembership()
    {
        const string question =
            "Quali sono gli attori che doppiano i cani protagonisti del film Isola dei cani?";

        var handler =
            new RecordingHandler(
                """
                {
                  "choices": [
                    {
                      "message": {
                        "role": "assistant",
                        "content": "{\"queries\":[\"Isle of Dogs principal cast character actors\",\"Isle of Dogs Hero Pack dog characters\"]}"
                      }
                    }
                  ]
                }
                """);

        using var httpClient =
            new HttpClient(handler)
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

        Assert.True(result.Count >= 2);
        Assert.Contains(
            "principal cast",
            result[0],
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "protagonist",
            result[0],
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Hero Pack",
            result[0],
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            result,
            query =>
                query.Contains(
                    "Hero Pack",
                    StringComparison.OrdinalIgnoreCase));

        using var requestJson =
            JsonDocument.Parse(
                Assert.IsType<string>(handler.RequestBody));

        var systemPrompt =
            requestJson.RootElement
                .GetProperty("messages")[0]
                .GetProperty("content")
                .GetString();

        Assert.NotNull(systemPrompt);
        Assert.Contains(
            "entity-to-role",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "category membership",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "separate retrieval queries",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "first focused query",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "without the category qualifier",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "canonical mapping or list headings",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "principal cast",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "character-to-actor",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
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
