namespace CafeHub.Application.Pagination
{
    public sealed class InvalidPaginationConfigurationException : InvalidOperationException
    {
        public InvalidPaginationConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {PaginationConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
