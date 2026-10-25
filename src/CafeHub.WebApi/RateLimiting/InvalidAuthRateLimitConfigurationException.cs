namespace CafeHub.WebApi.RateLimiting
{
    public sealed class InvalidAuthRateLimitConfigurationException : InvalidOperationException
    {
        public InvalidAuthRateLimitConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {AuthRateLimitConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
