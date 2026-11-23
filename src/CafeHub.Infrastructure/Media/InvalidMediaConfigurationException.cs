namespace CafeHub.Infrastructure.Media
{
    public sealed class InvalidMediaConfigurationException : InvalidOperationException
    {
        public InvalidMediaConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {MediaConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
