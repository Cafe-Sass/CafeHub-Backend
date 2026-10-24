namespace CafeHub.Infrastructure.Authentication
{
    public sealed class InvalidJwtConfigurationException : InvalidOperationException
    {
        public InvalidJwtConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {JwtConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
