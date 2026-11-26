using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Inventory
{
    internal sealed class InventoryAlertStore(TenantDbContext context) : IInventoryAlertStore
    {
        public async Task<InventoryAlertListPage> ListAsync(
            InventoryAlertListRequest request, CancellationToken cancellationToken)
        {
            var query = context.InventoryAlerts.AsNoTracking();
            if (request.BranchId is { } branchId)
            {
                query = query.Where(alert => alert.BranchId == branchId);
            }

            if (request.Kind is { } kind)
            {
                query = query.Where(alert => alert.Kind == kind);
            }

            if (request.State is { } state)
            {
                query = query.Where(alert => alert.State == state);
            }

            int? totalCount = null;
            if (request.IncludeTotalCount)
            {
                totalCount = await query.CountAsync(cancellationToken);
            }

            var fetched = await query
                .OrderByDescending(alert => alert.RaisedAt)
                .ThenByDescending(alert => alert.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            var itemIds = fetched.Select(alert => alert.ItemId).Distinct().ToList();
            var branchIds = fetched.Select(alert => alert.BranchId).Distinct().ToList();
            var batchIds = fetched
                .Where(alert => alert.BatchId is not null)
                .Select(alert => alert.BatchId!.Value)
                .Distinct()
                .ToList();

            var itemNames = itemIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await context.InventoryItems.AsNoTracking()
                    .Where(item => itemIds.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

            var batches = batchIds.Count == 0
                ? new Dictionary<Guid, InventoryBatch>()
                : await context.InventoryBatches.AsNoTracking()
                    .Where(batch => batchIds.Contains(batch.Id))
                    .ToDictionaryAsync(batch => batch.Id, cancellationToken);

            var totals = itemIds.Count == 0
                ? new Dictionary<(Guid, Guid), decimal>()
                : (await context.InventoryBatches.AsNoTracking()
                    .Where(batch => itemIds.Contains(batch.ItemId) && branchIds.Contains(batch.BranchId))
                    .GroupBy(batch => new { batch.ItemId, batch.BranchId })
                    .Select(group => new
                    {
                        group.Key.ItemId,
                        group.Key.BranchId,
                        Total = group.Sum(batch => batch.RemainingQuantity),
                    })
                    .ToListAsync(cancellationToken))
                .ToDictionary(row => (row.ItemId, row.BranchId), row => row.Total);

            var items = fetched
                .Select(alert => Map(alert, itemNames, batches, totals))
                .ToList();

            return new InventoryAlertListPage(items, totalCount);
        }

        private static InventoryAlertResponse Map(
            InventoryAlert alert,
            IReadOnlyDictionary<Guid, string> itemNames,
            IReadOnlyDictionary<Guid, InventoryBatch> batches,
            IReadOnlyDictionary<(Guid, Guid), decimal> totals)
        {
            var itemName = itemNames.TryGetValue(alert.ItemId, out var name)
                ? name
                : string.Empty;
            var batch = alert.BatchId is { } batchId && batches.TryGetValue(batchId, out var stored)
                ? stored
                : null;

            decimal? quantity = alert.Kind == InventoryAlertKind.LowStock
                ? totals.TryGetValue((alert.ItemId, alert.BranchId), out var total) ? total : 0m
                : batch?.RemainingQuantity;

            return new InventoryAlertResponse(
                alert.Id,
                alert.ItemId,
                itemName,
                alert.BranchId,
                alert.BatchId,
                alert.Kind.ToString(),
                alert.State.ToString(),
                quantity,
                alert.Kind == InventoryAlertKind.LowStock ? null : batch?.ExpiryDate,
                alert.RaisedAt,
                alert.ResolvedAt,
                alert.NotifiedAt);
        }
    }
}
