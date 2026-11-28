namespace CafeHub.Infrastructure.Discovery
{
    public sealed class InvalidDiscoveryConfigurationException : InvalidOperationException
    {
        public InvalidDiscoveryConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {DiscoveryConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
