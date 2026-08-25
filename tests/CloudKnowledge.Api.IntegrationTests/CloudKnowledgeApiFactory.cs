using CloudKnowledge.Application.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CloudKnowledge.Api.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Authentication;

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
                            "documents"
                    });
            });

        builder.ConfigureServices(
            services =>
            {
                services.RemoveAll<IDocumentProcessingQueue>();

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
}