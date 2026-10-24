namespace CafeHub.Infrastructure.Authentication
{
    public sealed class RefreshTokenOptions
    {
        public const int DefaultLifetimeDays = 7;
        public const int MinLifetimeDays = 1;
        public const int MaxLifetimeDays = 90;

        public int LifetimeDays { get; set; } = DefaultLifetimeDays;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (LifetimeDays < MinLifetimeDays || LifetimeDays > MaxLifetimeDays)
            {
                problems.Add(
                    $"{RefreshTokenConfigurationKeys.LifetimeDaysPath} must be between {MinLifetimeDays} and {MaxLifetimeDays} but was {LifetimeDays}.");
            }

            return problems;
        }
    }
}
