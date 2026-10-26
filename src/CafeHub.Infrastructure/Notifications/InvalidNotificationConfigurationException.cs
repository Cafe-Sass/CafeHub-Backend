namespace CafeHub.Infrastructure.Notifications
{
    public sealed class InvalidNotificationConfigurationException : InvalidOperationException
    {
        public InvalidNotificationConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            return $"The {NotificationConfigurationKeys.SectionName} options configuration is invalid: {string.Join(" ", problems)}";
        }
    }
}
