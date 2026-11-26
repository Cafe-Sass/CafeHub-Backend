using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Alerts;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Inventory
{
    internal sealed class InventoryAlertEvaluator(
        TenantDbContext context,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider,
        InventoryAlertNotifier notifier) : IInventoryAlertEvaluator
    {
        private const string OpenBatchIndex = "uq_inventory_alerts_open_batch";
        private const string OpenItemIndex = "uq_inventory_alerts_open_item";

        public async Task<Result> EvaluateStockAsync(
            Guid branchId, Guid itemId, CancellationToken cancellationToken)
        {
            var item = await context.InventoryItems.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);
            if (item is null)
            {
                return Result.Success();
            }

            var remaining = (await context.InventoryBatches.AsNoTracking()
                .Where(batch => batch.BranchId == branchId && batch.ItemId == itemId)
                .SumAsync(batch => (decimal?)batch.RemainingQuantity, cancellationToken)) ?? 0m;

            var pending = new List<PendingAlert>();
            if (InventoryAlertRule.IsLowStock(remaining, item.LowStockThreshold))
            {
                pending.Add(new PendingAlert(
                    new InventoryAlertCondition(itemId, branchId, null, InventoryAlertKind.LowStock),
                    Values(item.Name, branchId, InventoryAlertKind.LowStock, remaining, null)));
            }

            var openAlerts = await context.InventoryAlerts
                .Where(alert => alert.State == InventoryAlertState.Open
                    && alert.BranchId == branchId
                    && alert.ItemId == itemId)
                .ToListAsync(cancellationToken);

            return await ApplyAsync(openAlerts, pending, cancellationToken);
        }

        public async Task<Result> EvaluateExpiryAsync(
            Guid? branchId, Guid? itemId, CancellationToken cancellationToken)
        {
            var batchQuery = context.InventoryBatches.AsNoTracking()
                .Where(batch => batch.RemainingQuantity > 0m && batch.ExpiryDate != null);
            if (branchId is { } selectedBranchId)
            {
                batchQuery = batchQuery.Where(batch => batch.BranchId == selectedBranchId);
            }

            if (itemId is { } selectedItemId)
            {
                batchQuery = batchQuery.Where(batch => batch.ItemId == selectedItemId);
            }

            var batches = await batchQuery.ToListAsync(cancellationToken);

            var alertQuery = context.InventoryAlerts
                .Where(alert => alert.State == InventoryAlertState.Open);
            if (branchId is { } scopedBranchId)
            {
                alertQuery = alertQuery.Where(alert => alert.BranchId == scopedBranchId);
            }

            if (itemId is { } scopedItemId)
            {
                alertQuery = alertQuery.Where(alert => alert.ItemId == scopedItemId);
            }

            var openAlerts = await alertQuery.ToListAsync(cancellationToken);

            var pending = new List<PendingAlert>();
            var itemIds = batches.Select(batch => batch.ItemId).Distinct().ToList();
            if (itemIds.Count > 0)
            {
                var items = await context.InventoryItems.AsNoTracking()
                    .Where(item => itemIds.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id, cancellationToken);
                var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

                foreach (var batch in batches)
                {
                    if (!items.TryGetValue(batch.ItemId, out var item))
                    {
                        continue;
                    }

                    if (InventoryAlertRule.ClassifyExpiry(
                            batch.ExpiryDate, today, item.NearExpiryDays) is not { } kind)
                    {
                        continue;
                    }

                    pending.Add(new PendingAlert(
                        new InventoryAlertCondition(batch.ItemId, batch.BranchId, batch.Id, kind),
                        Values(item.Name, batch.BranchId, kind, batch.RemainingQuantity, batch.ExpiryDate)));
                }
            }

            return await ApplyAsync(openAlerts, pending, cancellationToken);
        }

        private async Task<Result> ApplyAsync(
            IReadOnlyList<InventoryAlert> openAlerts,
            IReadOnlyList<PendingAlert> pending,
            CancellationToken cancellationToken)
        {
            var plan = InventoryAlertRule.Plan(
                openAlerts, pending.Select(alert => alert.Condition));
            if (plan.Raise.Count == 0 && plan.Resolve.Count == 0)
            {
                return Result.Success();
            }

            var now = timeProvider.GetUtcNow();
            var actorId = currentUser.UserId;
            foreach (var alert in plan.Resolve)
            {
                alert.Resolve(now, actorId);
            }

            var values = new Dictionary<InventoryAlertCondition, IReadOnlyDictionary<string, string>>();
            foreach (var alert in pending)
            {
                values.TryAdd(alert.Condition, alert.Values);
            }

            var raised = new List<(InventoryAlert Alert, IReadOnlyDictionary<string, string> Values)>();
            foreach (var condition in plan.Raise)
            {
                var alert = InventoryAlert.Raise(
                    Guid.CreateVersion7(),
                    condition.ItemId,
                    condition.BranchId,
                    condition.BatchId,
                    condition.Kind,
                    now,
                    actorId);
                context.InventoryAlerts.Add(alert);
                raised.Add((alert, values[condition]));
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            } postgres
                && postgres.ConstraintName is OpenBatchIndex or OpenItemIndex)
            {
                context.ChangeTracker.Clear();
                return Result.Success();
            }

            if (raised.Count == 0)
            {
                return Result.Success();
            }

            var recipientEmail = await RecipientEmailAsync(cancellationToken);
            var notified = false;
            foreach (var (alert, alertValues) in raised)
            {
                if (await notifier.TrySendAsync(
                        alert, currentUser.TenantId, recipientEmail, alertValues, cancellationToken))
                {
                    alert.MarkNotified(timeProvider.GetUtcNow(), actorId);
                    notified = true;
                }
            }

            if (notified)
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }

        private async Task<string?> RecipientEmailAsync(CancellationToken cancellationToken)
        {
            if (currentUser.UserId is not { } userId)
            {
                return null;
            }

            return await context.TenantUsers.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.Email)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static IReadOnlyDictionary<string, string> Values(
            string itemName,
            Guid branchId,
            InventoryAlertKind kind,
            decimal quantity,
            DateOnly? expiryDate)
        {
            var values = new Dictionary<string, string>
            {
                ["itemName"] = itemName,
                ["branchId"] = branchId.ToString(),
                ["alertKind"] = kind.ToString(),
                ["quantity"] = InventoryItem.FormatQuantity(quantity),
            };

            if (expiryDate is { } date)
            {
                values["expiryDate"] = InventoryBatch.FormatDate(date);
            }

            return values;
        }

        private sealed record PendingAlert(
            InventoryAlertCondition Condition,
            IReadOnlyDictionary<string, string> Values);
    }
}
