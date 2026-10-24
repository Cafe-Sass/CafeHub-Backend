namespace CafeHub.Infrastructure.Authentication
{
    public sealed class InvalidRefreshTokenConfigurationException : InvalidOperationException
    {
        public InvalidRefreshTokenConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {RefreshTokenConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
