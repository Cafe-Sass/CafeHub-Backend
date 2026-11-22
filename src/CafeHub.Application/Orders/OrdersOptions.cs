namespace CafeHub.Application.Orders
{
    public sealed class OrdersOptions
    {
        public const int MinimumResponseWindowSeconds = 30;
        public const int MaximumResponseWindowSeconds = 3600;

        public int ResponseWindowSeconds { get; set; } = OrdersConfigurationKeys.DefaultResponseWindowSeconds;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            if (ResponseWindowSeconds < MinimumResponseWindowSeconds
                || ResponseWindowSeconds > MaximumResponseWindowSeconds)
            {
                problems.Add(
                    $"{OrdersConfigurationKeys.ResponseWindowSecondsPath} must be between {MinimumResponseWindowSeconds} and {MaximumResponseWindowSeconds} but was {ResponseWindowSeconds}.");
            }

            return problems;
        }
    }
}
