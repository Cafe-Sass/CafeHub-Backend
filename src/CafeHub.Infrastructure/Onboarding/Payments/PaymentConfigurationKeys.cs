namespace CafeHub.Infrastructure.Onboarding.Payments
{
    public static class PaymentConfigurationKeys
    {
        public const string SectionName = "Payments";
        public const string ProviderKey = "Provider";
        public const string MockSectionKey = "Mock";
        public const string MockOutcomeKey = "Outcome";
        public const string MockAllowOutsideDevelopmentKey = "AllowOutsideDevelopment";

        public const string ProviderPath = $"{SectionName}:{ProviderKey}";
        public const string MockOutcomePath = $"{SectionName}:{MockSectionKey}:{MockOutcomeKey}";
        public const string MockAllowOutsideDevelopmentPath = $"{SectionName}:{MockSectionKey}:{MockAllowOutsideDevelopmentKey}";
    }
}
