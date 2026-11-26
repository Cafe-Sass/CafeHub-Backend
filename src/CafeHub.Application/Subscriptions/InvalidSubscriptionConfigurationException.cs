namespace CafeHub.Application.Subscriptions
{
    public sealed class InvalidSubscriptionConfigurationException : InvalidOperationException
    {
        public InvalidSubscriptionConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {SubscriptionConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
