using System.Net;
using System.Text;
using System.Text.Json;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Infrastructure.Documents;

namespace CloudKnowledge.Infrastructure.Tests.Documents;

public sealed class GroundedAnswerPromptTests
{
    [Fact]
    public async Task OpenAiAnswerGenerator_ShouldTreatMissingRestrictionAsUnknownNotPermission()
    {
        var handler =
            new RecordingHandler();

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri("https://api.openai.com/")
            };

        var sut =
            new OpenAiAnswerGenerator(
                httpClient,
                model: "gpt-4.1-nano",
                apiKey: "test-key",
                temperature: 0.1,
                maxTokens: 256);

        await sut.GenerateAsync(
            "Posso mantenere la corrente nominale completa?",
            [
                new AnswerContextSource(
                    "S1",
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    0,
                    "Installation altitude 0...4000 m.")
            ],
            CancellationToken.None);

        using var requestJson =
            JsonDocument.Parse(
                Assert.IsType<string>(
                    handler.RequestBody));

        var systemPrompt =
            requestJson.RootElement
                .GetProperty("messages")[0]
                .GetProperty("content")
                .GetString();

        Assert.NotNull(systemPrompt);

        Assert.Contains(
            "assenza",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "non è una prova",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingHandler
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

            return new HttpResponseMessage(
                HttpStatusCode.OK)
            {
                Content =
                    new StringContent(
                        """
                        {
                          "choices": [
                            {
                              "message": {
                                "role": "assistant",
                                "content": "Informazione insufficiente [S1]."
                              }
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
            };
        }
    }
}
