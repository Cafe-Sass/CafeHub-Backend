namespace CafeHub.Application.Orders
{
    public sealed class InvalidOrdersConfigurationException : InvalidOperationException
    {
        public InvalidOrdersConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {OrdersConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
