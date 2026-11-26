using CafeHub.Application.Notifications;
using CafeHub.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace CafeHub.Application.Inventory.Alerts
{
    public sealed class InventoryAlertNotifier(
        INotificationSender sender, ILogger<InventoryAlertNotifier> logger)
    {
        public async Task<bool> TrySendAsync(
            InventoryAlert alert,
            Guid? tenantId,
            string? recipientEmail,
            IReadOnlyDictionary<string, string> values,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(alert);
            ArgumentNullException.ThrowIfNull(values);

            if (tenantId is not { } resolvedTenantId
                || resolvedTenantId == Guid.Empty
                || string.IsNullOrWhiteSpace(recipientEmail))
            {
                logger.LogWarning(
                    "The inventory alert {AlertId} was kept unsent because no tenant recipient is available.",
                    alert.Id);
                return false;
            }

            NotificationMessage message;
            try
            {
                message = new NotificationMessage(
                    NotificationKind.InventoryAlert,
                    resolvedTenantId,
                    recipientEmail,
                    NotificationTemplateKeys.InventoryAlert,
                    values);
            }
            catch (ArgumentException exception)
            {
                logger.LogWarning(
                    exception,
                    "The inventory alert {AlertId} was kept unsent because its message is invalid.",
                    alert.Id);
                return false;
            }

            try
            {
                var result = await sender.SendAsync(message, cancellationToken);
                if (result.IsSuccess)
                {
                    return true;
                }

                logger.LogWarning(
                    "The inventory alert {AlertId} was kept unsent because the send failed with code {ErrorCode}.",
                    alert.Id,
                    result.Error!.Code);
                return false;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "The inventory alert {AlertId} was kept unsent because the send failed.",
                    alert.Id);
                return false;
            }
        }
    }
}
