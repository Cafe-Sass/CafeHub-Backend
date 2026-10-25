using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Onboarding.Payments
{
    public static class PaymentOptionsBinder
    {
        public static PaymentOptions Bind(IConfiguration? configuration, bool isDevelopment)
        {
            var section = configuration?.GetSection(PaymentConfigurationKeys.SectionName);
            var options = new PaymentOptions();
            var problems = new List<string>();

            options.Provider = section?[PaymentConfigurationKeys.ProviderKey] ?? "Mock";

            if (!string.Equals(options.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{PaymentConfigurationKeys.ProviderPath}: The configured provider is not supported. Only 'Mock' is available.");
            }

            var mockSection = section?.GetSection(PaymentConfigurationKeys.MockSectionKey);
            options.Mock.Outcome = mockSection?[PaymentConfigurationKeys.MockOutcomeKey] ?? "Succeed";

            var allowRaw = mockSection?[PaymentConfigurationKeys.MockAllowOutsideDevelopmentKey];
            if (bool.TryParse(allowRaw, out var allow))
            {
                options.Mock.AllowOutsideDevelopment = allow;
            }

            if (!isDevelopment && !options.Mock.AllowOutsideDevelopment)
            {
                problems.Add($"{PaymentConfigurationKeys.MockAllowOutsideDevelopmentPath}: Mock payment gateway is only allowed in Development unless {PaymentConfigurationKeys.MockAllowOutsideDevelopmentKey} is set to true.");
            }

            if (problems.Count > 0)
            {
                throw new InvalidPaymentConfigurationException(problems);
            }

            return options;
        }
    }
}
