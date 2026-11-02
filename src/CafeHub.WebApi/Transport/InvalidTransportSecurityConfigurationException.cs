namespace CafeHub.WebApi.Transport
{
    public sealed class InvalidTransportSecurityConfigurationException : InvalidOperationException
    {
        public InvalidTransportSecurityConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {TransportSecurityConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
