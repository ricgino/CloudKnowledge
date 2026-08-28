using CloudKnowledge.Api.IntegrationTests.Authentication;
using CloudKnowledge.Application.Documents;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Application.Notifications.DocumentReady;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudKnowledge.Api.IntegrationTests;

public sealed class CloudKnowledgeApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _storageConnectionString;

    public FakeDocumentProcessingQueue ProcessingQueue { get; } = new();

    public CloudKnowledgeApiFactory(
        string postgresConnectionString,
        string storageConnectionString)
    {
        _postgresConnectionString =
            postgresConnectionString;

        _storageConnectionString =
            storageConnectionString;
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            _postgresConnectionString,

                        ["Storage:ConnectionString"] =
                            _storageConnectionString,

                        ["Storage:ContainerName"] =
                            "documents",

                        ["Messaging:NotificationsEnabled"] =
                            "false"
                    });
            });

        builder.ConfigureServices(
            services =>
            {
                services.RemoveAll<IDocumentProcessingQueue>();
                services.RemoveAll<IDocumentReadyPublisher>();
                services.RemoveAll<IEmbeddingGenerator>();
                services.RemoveAll<IAnswerGenerator>();

                services
                    .AddAuthentication(
                        options =>
                        {
                            options.DefaultAuthenticateScheme =
                                TestAuthenticationHandler.SchemeName;

                            options.DefaultChallengeScheme =
                                TestAuthenticationHandler.SchemeName;
                        })
                    .AddScheme<
                        AuthenticationSchemeOptions,
                        TestAuthenticationHandler>(
                            TestAuthenticationHandler.SchemeName,
                            _ =>
                            {
                            });

                services.AddSingleton<IDocumentProcessingQueue>(
                    ProcessingQueue);

                services.AddSingleton<IDocumentReadyPublisher>(
                    new FakeDocumentReadyPublisher());

                services.AddSingleton<IEmbeddingGenerator>(
                    new FakeEmbeddingGenerator());

                services.AddSingleton<IAnswerGenerator>(
                    new FakeAnswerGenerator());
            });
    }

    public sealed class FakeDocumentProcessingQueue
        : IDocumentProcessingQueue
    {
        public Guid? PublishedDocumentId { get; private set; }

        public Task PublishAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            PublishedDocumentId = documentId;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeDocumentReadyPublisher
        : IDocumentReadyPublisher
    {
        public Task PublishAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmbeddingGenerator
        : IEmbeddingGenerator
    {
        public int Dimensions =>
            768;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<float[]> embeddings =
                inputs
                    .Select(
                        _ =>
                        {
                            var vector =
                                new float[768];

                            vector[0] =
                                1.0f;

                            return vector;
                        })
                    .ToArray();

            return Task.FromResult(
                embeddings);
        }
    }

    private sealed class FakeAnswerGenerator
        : IAnswerGenerator
    {
        public Task<string> GenerateAsync(
            string question,
            IReadOnlyList<AnswerContextSource> sources,
            CancellationToken cancellationToken)
        {
            var labels =
                string.Join(
                    ", ",
                    sources.Select(
                        source => $"[{source.Label}]"));

            return Task.FromResult(
                $"Deterministic integration answer {labels}".Trim());
        }
    }
}
