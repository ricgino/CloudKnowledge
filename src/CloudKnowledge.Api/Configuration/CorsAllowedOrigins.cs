using Microsoft.Extensions.Configuration;

namespace CloudKnowledge.Api.Configuration;

public static class CorsAllowedOrigins
{
    public static string[] Get(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? [];
    }
}
