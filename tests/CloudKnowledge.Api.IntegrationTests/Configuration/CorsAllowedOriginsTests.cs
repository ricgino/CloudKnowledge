using CloudKnowledge.Api.Configuration;
using Microsoft.Extensions.Configuration;

namespace CloudKnowledge.Api.IntegrationTests.Configuration;

public sealed class CorsAllowedOriginsTests
{
    [Fact]
    public void Get_ShouldReturnEmpty_WhenNoOriginsAreConfigured()
    {
        var configuration = new ConfigurationManager();

        var result = CorsAllowedOrigins.Get(configuration);

        Assert.Empty(result);
    }

    [Fact]
    public void Get_ShouldReturnConfiguredOrigins()
    {
        var configuration = new ConfigurationManager
        {
            ["Cors:AllowedOrigins:0"] = "http://localhost:4200",
            ["Cors:AllowedOrigins:1"] = "https://example.test"
        };

        var result = CorsAllowedOrigins.Get(configuration);

        Assert.Equal(
            ["http://localhost:4200", "https://example.test"],
            result);
    }
}
