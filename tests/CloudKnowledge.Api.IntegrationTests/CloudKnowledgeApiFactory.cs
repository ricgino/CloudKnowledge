using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CloudKnowledge.Api.IntegrationTests;

public sealed class CloudKnowledgeApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _storageConnectionString;

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
    }
}