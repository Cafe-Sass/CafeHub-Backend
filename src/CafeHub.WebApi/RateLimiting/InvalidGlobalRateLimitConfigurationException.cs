namespace CafeHub.WebApi.RateLimiting
{
    public sealed class InvalidGlobalRateLimitConfigurationException : InvalidOperationException
    {
        public InvalidGlobalRateLimitConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {GlobalRateLimitConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
