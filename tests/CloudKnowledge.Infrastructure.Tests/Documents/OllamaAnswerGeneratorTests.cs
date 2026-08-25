using System.Net;
using System.Text;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Infrastructure.Documents;

namespace CloudKnowledge.Infrastructure.Tests.Documents;

public sealed class OllamaAnswerGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_WhenOllamaReturnsThinking_ShouldReturnOnlyFinalAnswer()
    {
        // Arrange
        var handler =
            new FakeOllamaHandler();

        var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri("http://localhost:11434")
            };

        var sut =
            new OllamaAnswerGenerator(
                httpClient,
                "qwen3:4b");

        var sources =
            new[]
            {
                new AnswerContextSource(
                    "S1",
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    0,
                    "Alperia Smart Services Srl.")
            };

        // Act
        var result =
            await sut.GenerateAsync(
                "Qual è l'azienda?",
                sources,
                CancellationToken.None);

        // Assert
        Assert.Equal(
            "Alperia Smart Services Srl. [S1]",
            result);

        Assert.DoesNotContain(
            "<think>",
            result);

        Assert.DoesNotContain(
            "internal reasoning",
            result);

        Assert.NotNull(
            handler.RequestBody);

        Assert.Contains(
            "\"think\":false",
            handler.RequestBody!.ToLowerInvariant());

        Assert.Contains(
            "/no_think",
            handler.RequestBody);
    }

    private sealed class FakeOllamaHandler
        : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody =
                await request.Content!
                    .ReadAsStringAsync(
                        cancellationToken);

            const string responseJson =
                """
                {
                  "message": {
                    "role": "assistant",
                    "content": "<think>internal reasoning</think>\nAlperia Smart Services Srl. [S1]"
                  }
                }
                """;

            return new HttpResponseMessage(
                HttpStatusCode.OK)
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