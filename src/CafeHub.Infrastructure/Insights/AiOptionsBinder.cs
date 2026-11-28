using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Insights
{
    public static class AiOptionsBinder
    {
        public static AiOptions Bind(IConfiguration? configuration)
        {
            var section = configuration
                ?.GetSection(AiInsightsConfigurationKeys.SectionName)
                .GetSection(AiInsightsConfigurationKeys.InsightsSectionName);
            var options = new AiOptions();
            var provider = section?[AiInsightsConfigurationKeys.ProviderKey];

            if (!string.IsNullOrWhiteSpace(provider))
            {
                options.InsightsProvider = provider;
            }

            if (!string.Equals(options.InsightsProvider, AiInsightsProviders.None, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidAiConfigurationException(
                    new[]
                    {
                        $"{AiInsightsConfigurationKeys.ProviderPath}: The configured provider is not supported. Only 'None' is available until the AI service is published.",
                    });
            }

            return options;
        }
    }
}
