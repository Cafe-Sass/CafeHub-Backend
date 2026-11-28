using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Discovery
{
    public static class DiscoveryOptionsBinder
    {
        public static DiscoveryOptions Bind(IConfiguration? configuration)
        {
            var section = configuration
                ?.GetSection(DiscoveryConfigurationKeys.SectionName)
                .GetSection(DiscoveryConfigurationKeys.DiscoverySectionName);
            var options = new DiscoveryOptions();
            var provider = section?[DiscoveryConfigurationKeys.ProviderKey];

            if (!string.IsNullOrWhiteSpace(provider))
            {
                options.Provider = provider;
            }

            if (!string.Equals(options.Provider, DiscoveryProviders.None, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDiscoveryConfigurationException(
                    new[]
                    {
                        $"{DiscoveryConfigurationKeys.ProviderPath}: The configured provider is not supported. Only 'None' is available until the AI service is published.",
                    });
            }

            return options;
        }
    }
}
