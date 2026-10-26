using CafeHub.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Notifications.Infrastructure
{
    public sealed class NotificationOptionsBinderTests
    {
        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            var prefixed = settings.ToDictionary(
                pair => $"{NotificationConfigurationKeys.SectionName}:{pair.Key}",
                pair => pair.Value);

            return new ConfigurationBuilder()
                .AddInMemoryCollection(prefixed)
                .Build();
        }

        [Fact]
        public void Bind_MissingSection_DefaultsToLogProvider()
        {
            var options = NotificationOptionsBinder.Bind(new ConfigurationBuilder().Build());

            Assert.Equal("Log", options.Provider);
        }

        [Theory]
        [InlineData("Log")]
        [InlineData("log")]
        public void Bind_LogProvider_IsAccepted(string provider)
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [NotificationConfigurationKeys.ProviderKey] = provider,
            });

            var options = NotificationOptionsBinder.Bind(configuration);

            Assert.Equal(provider, options.Provider);
        }

        [Theory]
        [InlineData("SendGrid")]
        [InlineData("Email")]
        [InlineData("Sms")]
        [InlineData("None")]
        public void Bind_UnsupportedProvider_FailsStartupWithSafeMessage(string provider)
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [NotificationConfigurationKeys.ProviderKey] = provider,
            });

            var exception = Assert.Throws<InvalidNotificationConfigurationException>(
                () => NotificationOptionsBinder.Bind(configuration));

            Assert.Contains(
                NotificationConfigurationKeys.ProviderPath,
                exception.Problems.Single(),
                StringComparison.Ordinal);
            Assert.Contains("Only 'Log' is available", exception.Problems.Single(), StringComparison.Ordinal);
        }
    }
}
