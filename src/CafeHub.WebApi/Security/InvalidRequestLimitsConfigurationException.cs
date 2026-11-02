namespace CafeHub.WebApi.Security
{
    public sealed class InvalidRequestLimitsConfigurationException : InvalidOperationException
    {
        public InvalidRequestLimitsConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {RequestLimitsConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
