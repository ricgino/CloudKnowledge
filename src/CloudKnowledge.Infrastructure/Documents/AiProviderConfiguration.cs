using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed record AiProviderConfiguration(
    string Provider,
    Uri BaseUrl,
    string? ApiKey,
    string EmbeddingModel,
    string? AnswerModel,
    int EmbeddingDimensions,
    double AnswerTemperature,
    int AnswerMaxTokens)
{
    public const string OllamaProvider = "Ollama";
    public const string AzureOpenAiProvider = "AzureOpenAI";

    public bool IsAzureOpenAi =>
        string.Equals(
            Provider,
            AzureOpenAiProvider,
            StringComparison.Ordinal);

    public static AiProviderConfiguration From(
        IConfiguration configuration,
        bool requireAnswerGenerator)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var rawProvider =
            configuration["Ai:Provider"];

        var provider =
            string.IsNullOrWhiteSpace(rawProvider)
                ? OllamaProvider
                : rawProvider.Trim();

        if (string.Equals(
                provider,
                OllamaProvider,
                StringComparison.OrdinalIgnoreCase))
        {
            provider = OllamaProvider;
        }
        else if (string.Equals(
                     provider,
                     AzureOpenAiProvider,
                     StringComparison.OrdinalIgnoreCase))
        {
            provider = AzureOpenAiProvider;
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported AI provider '{provider}'. " +
                $"Supported providers are {OllamaProvider} and {AzureOpenAiProvider}.");
        }

        var dimensions =
            GetPositiveInt(
                configuration,
                "Ai:EmbeddingDimensions");

        var temperature =
            GetDouble(
                configuration,
                "Ai:AnswerTemperature",
                defaultValue: 0.1);

        if (temperature < 0 || temperature > 2)
        {
            throw new InvalidOperationException(
                "Ai:AnswerTemperature must be between 0 and 2.");
        }

        var maxTokens =
            GetPositiveInt(
                configuration,
                "Ai:AnswerMaxTokens",
                defaultValue: 256);

        if (provider == OllamaProvider)
        {
            var baseUrl =
                GetRequiredUri(
                    configuration,
                    "Ai:BaseUrl");

            var embeddingModel =
                GetRequired(
                    configuration,
                    "Ai:EmbeddingModel");

            var answerModel =
                requireAnswerGenerator
                    ? GetRequired(
                        configuration,
                        "Ai:AnswerModel")
                    : configuration["Ai:AnswerModel"];

            return new AiProviderConfiguration(
                provider,
                baseUrl,
                ApiKey: null,
                embeddingModel,
                answerModel,
                dimensions,
                temperature,
                maxTokens);
        }

        var endpoint =
            GetRequiredUri(
                configuration,
                "Ai:Endpoint");

        var apiKey =
            GetRequired(
                configuration,
                "Ai:ApiKey");

        var embeddingDeployment =
            GetRequired(
                configuration,
                "Ai:EmbeddingDeployment");

        var answerDeployment =
            requireAnswerGenerator
                ? GetRequired(
                    configuration,
                    "Ai:AnswerDeployment")
                : configuration["Ai:AnswerDeployment"];

        return new AiProviderConfiguration(
            provider,
            endpoint,
            apiKey,
            embeddingDeployment,
            answerDeployment,
            dimensions,
            temperature,
            maxTokens);
    }

    private static string GetRequired(
        IConfiguration configuration,
        string key)
    {
        var value =
            configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required configuration '{key}' was not found.");
        }

        return value.Trim();
    }

    private static Uri GetRequiredUri(
        IConfiguration configuration,
        string key)
    {
        var value =
            GetRequired(
                configuration,
                key);

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri))
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must be an absolute URI.");
        }

        return uri;
    }

    private static int GetPositiveInt(
        IConfiguration configuration,
        string key,
        int? defaultValue = null)
    {
        var rawValue =
            configuration[key];

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

            throw new InvalidOperationException(
                $"Required configuration '{key}' was not found.");
        }

        if (!int.TryParse(
                rawValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value) ||
            value <= 0)
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must be a positive integer.");
        }

        return value;
    }

    private static double GetDouble(
        IConfiguration configuration,
        string key,
        double defaultValue)
    {
        var rawValue =
            configuration[key];

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return defaultValue;
        }

        if (!double.TryParse(
                rawValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must be a number.");
        }

        return value;
    }
}
