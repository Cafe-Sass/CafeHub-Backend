using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Notifications
{
    public static class NotificationOptionsBinder
    {
        public static NotificationOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(NotificationConfigurationKeys.SectionName);
            var options = new NotificationOptions();

            options.Provider = section?[NotificationConfigurationKeys.ProviderKey] ?? "Log";

            if (!string.Equals(options.Provider, "Log", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidNotificationConfigurationException(
                    new[]
                    {
                        $"{NotificationConfigurationKeys.ProviderPath}: The configured provider is not supported. Only 'Log' is available.",
                    });
            }

            return options;
        }
    }
}
