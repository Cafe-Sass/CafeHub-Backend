namespace CafeHub.Infrastructure.Identity
{
    public sealed class InvalidIdentityConfigurationException : InvalidOperationException
    {
        public InvalidIdentityConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {IdentityConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
