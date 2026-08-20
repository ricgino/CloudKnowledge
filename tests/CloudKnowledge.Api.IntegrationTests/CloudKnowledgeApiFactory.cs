using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CloudKnowledge.Api.IntegrationTests;

public sealed class CloudKnowledgeApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CloudKnowledgeApiFactory(
        string connectionString)
    {
        _connectionString = connectionString;
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
                            _connectionString
                    });
            });
    }
}