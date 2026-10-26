using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace CafeHub.Infrastructure.Notifications
{
    public sealed class LoggingNotificationSender : INotificationSender
    {
        private readonly ILogger<LoggingNotificationSender> logger;

        public LoggingNotificationSender(ILogger<LoggingNotificationSender> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<Result> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);

            logger.LogInformation(
                "Notification {NotificationKind} for tenant {TenantId} was recorded but not sent because the delivery provider is not configured.",
                message.Kind,
                message.TenantId);

            return Task.FromResult(Result.Success());
        }
    }
}
