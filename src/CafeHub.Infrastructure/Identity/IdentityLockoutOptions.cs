namespace CafeHub.Infrastructure.Identity
{
    /// <summary>
    /// Lockout thresholds for administrator password attempts, bound from the <c>Identity:Lockout</c>
    /// configuration section. The values are applied to the ASP.NET Core Identity lockout options, so
    /// every Super Admin account is protected against password guessing with the configured countdown.
    /// </summary>
    public sealed class IdentityLockoutOptions
    {
        public const int DefaultMaxFailedAccessAttempts = 5;
        public const int DefaultLockoutMinutes = 15;
        public const int MinMaxFailedAccessAttempts = 3;
        public const int MaxMaxFailedAccessAttempts = 20;
        public const int MinLockoutMinutes = 1;
        public const int MaxLockoutMinutes = 1440;

        public int MaxFailedAccessAttempts { get; set; } = DefaultMaxFailedAccessAttempts;

        public int LockoutMinutes { get; set; } = DefaultLockoutMinutes;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (MaxFailedAccessAttempts < MinMaxFailedAccessAttempts || MaxFailedAccessAttempts > MaxMaxFailedAccessAttempts)
            {
                problems.Add(
                    $"{IdentityConfigurationKeys.MaxFailedAccessAttemptsPath} must be between {MinMaxFailedAccessAttempts} and {MaxMaxFailedAccessAttempts} but was {MaxFailedAccessAttempts}.");
            }

            if (LockoutMinutes < MinLockoutMinutes || LockoutMinutes > MaxLockoutMinutes)
            {
                problems.Add(
                    $"{IdentityConfigurationKeys.LockoutMinutesPath} must be between {MinLockoutMinutes} and {MaxLockoutMinutes} but was {LockoutMinutes}.");
            }

            return problems;
        }
    }
}
