using System.Net;
using System.Text;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Infrastructure.Documents;
using Microsoft.Extensions.Logging;

namespace CloudKnowledge.Infrastructure.Tests.Documents;

public sealed class OllamaAnswerGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_ShouldBoundGenerationAndLogOllamaTiming()
    {
        var handler =
            new FakeOllamaHandler();

        var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri("http://localhost:11434")
            };

        var logger =
            new RecordingLogger<OllamaAnswerGenerator>();

        var sut =
            new OllamaAnswerGenerator(
                httpClient,
                "qwen3:4b",
                temperature: 0.1,
                maxTokens: 256,
                logger);

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

        var result =
            await sut.GenerateAsync(
                "Qual è l'azienda?",
                sources,
                CancellationToken.None);

        Assert.Equal(
            "Alperia Smart Services Srl. [S1]",
            result);

        Assert.DoesNotContain(
            "internal reasoning",
            result);

        Assert.NotNull(
            handler.RequestBody);

        var requestBody =
            handler.RequestBody!;

        Assert.Contains(
            "\"think\":false",
            requestBody.ToLowerInvariant());

        Assert.Contains(
            "/no_think",
            requestBody);

        Assert.Contains(
            "\"format\"",
            requestBody.ToLowerInvariant());

        Assert.Contains(
            "\"answer\"",
            requestBody.ToLowerInvariant());

        Assert.Contains(
            "\"temperature\":0.1",
            requestBody);

        Assert.Contains(
            "\"num_predict\":256",
            requestBody);

        Assert.Contains(
            logger.Messages,
            message =>
                message.Contains(
                    "promptTokens=1200",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            logger.Messages,
            message =>
                message.Contains(
                    "outputTokens=80",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            logger.Messages,
            message =>
                message.Contains(
                    "doneReason=stop",
                    StringComparison.OrdinalIgnoreCase));
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
                    "thinking": "internal reasoning",
                    "content": "{\"answer\":\"Alperia Smart Services Srl. [S1]\"}"
                  },
                  "total_duration": 4500000000,
                  "load_duration": 200000000,
                  "prompt_eval_count": 1200,
                  "prompt_eval_duration": 1500000000,
                  "eval_count": 80,
                  "eval_duration": 2700000000,
                  "done_reason": "stop"
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

    private sealed class RecordingLogger<T>
        : ILogger<T>
    {
        public List<string> Messages { get; } =
            new();

        public IDisposable? BeginScope<TState>(
            TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(
            LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(
                formatter(
                    state,
                    exception));
        }
    }
}
