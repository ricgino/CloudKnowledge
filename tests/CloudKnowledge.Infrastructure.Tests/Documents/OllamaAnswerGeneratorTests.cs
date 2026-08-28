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

    [Fact]
    public async Task GenerateAsync_ShouldRetryOnce_WhenFirstAnswerEchoesQuestion()
    {
        var handler =
            new SequenceOllamaHandler(
                CreateResponse(
                    "faceless void changes"),
                CreateResponse(
                    "Faceless Void's damage gain per level increased from 3.0 to 3.1. [S1]"));

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
                    "FACELESS VOID: Damage gain per level increased from 3.0 to 3.1.")
            };

        var result =
            await sut.GenerateAsync(
                "faceless void changes",
                sources,
                CancellationToken.None);

        Assert.Equal(
            "Faceless Void's damage gain per level increased from 3.0 to 3.1. [S1]",
            result);

        Assert.Equal(
            2,
            handler.RequestBodies.Count);

        Assert.Contains(
            "non ripetere la domanda",
            handler.RequestBodies[1],
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_ShouldAcceptSubstantiveAnswerWithoutInlineCitation()
    {
        var handler =
            new SequenceOllamaHandler(
                CreateResponse(
                    "Faceless Void's Time Walk radius decreased from 400 to 325."));

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
                    "FACELESS VOID: Time Walk radius decreased from 400 to 325.")
            };

        var result =
            await sut.GenerateAsync(
                "faceless void changes",
                sources,
                CancellationToken.None);

        Assert.Equal(
            "Faceless Void's Time Walk radius decreased from 400 to 325.",
            result);

        Assert.Single(
            handler.RequestBodies);
    }

    [Fact]
    public async Task GenerateAsync_ShouldRetry_WhenStructuredAnswerIsTruncated()
    {
        var handler =
            new SequenceOllamaHandler(
                CreateTruncatedResponse(),
                CreateResponse(
                    "Faceless Void's Time Walk radius decreased from 400 to 325. [S1]"));

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
                    "FACELESS VOID: Time Walk radius decreased from 400 to 325.")
            };

        var result =
            await sut.GenerateAsync(
                "faceless void changes",
                sources,
                CancellationToken.None);

        Assert.Equal(
            "Faceless Void's Time Walk radius decreased from 400 to 325. [S1]",
            result);

        Assert.Equal(
            2,
            handler.RequestBodies.Count);
    }

    private static string CreateResponse(
        string answer)
    {
        var escapedAnswer =
            answer
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);

        return
            $$"""
            {
              "message": {
                "role": "assistant",
                "content": "{\"answer\":\"{{escapedAnswer}}\"}"
              },
              "total_duration": 1000000000,
              "load_duration": 100000000,
              "prompt_eval_count": 300,
              "prompt_eval_duration": 400000000,
              "eval_count": 40,
              "eval_duration": 500000000,
              "done_reason": "stop"
            }
            """;
    }

    private static string CreateTruncatedResponse()
    {
        return
            """
            {
              "message": {
                "role": "assistant",
                "content": "{\"answer\":\"Faceless Void's Time Walk radius decreased from 400 to 325"
              },
              "total_duration": 1000000000,
              "load_duration": 100000000,
              "prompt_eval_count": 300,
              "prompt_eval_duration": 400000000,
              "eval_count": 256,
              "eval_duration": 500000000,
              "done_reason": "length"
            }
            """;
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

    private sealed class SequenceOllamaHandler
        : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public SequenceOllamaHandler(
            params string[] responses)
        {
            _responses =
                new Queue<string>(responses);
        }

        public List<string> RequestBodies { get; } =
            new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(
                await request.Content!
                    .ReadAsStringAsync(
                        cancellationToken));

            var responseJson =
                _responses.Dequeue();

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
