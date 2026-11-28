namespace CafeHub.Infrastructure.Insights
{
    public sealed class InvalidAiConfigurationException : InvalidOperationException
    {
        public InvalidAiConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {AiInsightsConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
