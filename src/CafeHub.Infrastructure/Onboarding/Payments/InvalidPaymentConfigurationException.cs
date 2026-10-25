namespace CafeHub.Infrastructure.Onboarding.Payments
{
    public sealed class InvalidPaymentConfigurationException : InvalidOperationException
    {
        public InvalidPaymentConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {PaymentConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
