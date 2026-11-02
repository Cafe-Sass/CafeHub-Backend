namespace CafeHub.WebApi.RateLimiting
{
    /// <summary>
    /// Configuration for the global fixed-window rate-limiting policy. A fixed window of
    /// <see cref="WindowSeconds"/> seconds admits at most <see cref="PermitLimit"/> requests per client IP;
    /// requests beyond the limit are rejected immediately and never queued.
    /// </summary>
    public sealed class GlobalRateLimitOptions
    {
        public const int DefaultPermitLimit = 300;
        public const int DefaultWindowSeconds = 60;
        public const int MinPermitLimit = 1;
        public const int MaxPermitLimit = 100000;
        public const int MinWindowSeconds = 1;
        public const int MaxWindowSeconds = 3600;

        public int PermitLimit { get; set; } = DefaultPermitLimit;

        public int WindowSeconds { get; set; } = DefaultWindowSeconds;

        public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (PermitLimit < MinPermitLimit || PermitLimit > MaxPermitLimit)
            {
                problems.Add(
                    $"{GlobalRateLimitConfigurationKeys.PermitLimitPath} must be between {MinPermitLimit} and {MaxPermitLimit}.");
            }

            if (WindowSeconds < MinWindowSeconds || WindowSeconds > MaxWindowSeconds)
            {
                problems.Add(
                    $"{GlobalRateLimitConfigurationKeys.WindowSecondsPath} must be between {MinWindowSeconds} and {MaxWindowSeconds}.");
            }

            return problems;
        }
    }
}
