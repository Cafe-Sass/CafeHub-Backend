using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Notifications
{
    public interface INotificationSender
    {
        Task<Result> SendAsync(NotificationMessage message, CancellationToken cancellationToken);
    }
}
