using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Inventory;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Inventory
{
    internal sealed class InventoryItemStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IInventoryItemStore
    {
        private const string TargetType = "inventory_items";
        private const string ActiveNameIndex = "uq_inventory_items_active_name";

        public async Task<Result<InventoryItemResponse>> CreateAsync(
            string name,
            InventoryUnit unit,
            decimal lowStockThreshold,
            bool tracksExpiry,
            int nearExpiryDays,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var item = InventoryItem.Create(
                Guid.CreateVersion7(),
                name,
                unit,
                lowStockThreshold,
                tracksExpiry,
                nearExpiryDays,
                timeProvider.GetUtcNow(),
                actorUserId);
            context.InventoryItems.Add(item);

            var changes = TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(InventoryItem.Name), null, item.Name),
                new TenantAuditChanges.FieldChange(nameof(InventoryItem.Unit), null, item.Unit.ToString()),
                new TenantAuditChanges.FieldChange(
                    nameof(InventoryItem.LowStockThreshold), null, InventoryItem.FormatQuantity(item.LowStockThreshold)),
                new TenantAuditChanges.FieldChange(
                    nameof(InventoryItem.TracksExpiry), null, item.TracksExpiry.ToString()),
                new TenantAuditChanges.FieldChange(
                    nameof(InventoryItem.NearExpiryDays),
                    null,
                    item.NearExpiryDays.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(nameof(InventoryItem.Status), null, item.Status.ToString()),
            });

            return await SaveAsync(item, actorUserId, "inventory_items.created.succeeded", changes, cancellationToken);
        }

        public async Task<Result<InventoryItemResponse>> UpdateAsync(
            Guid id,
            string name,
            InventoryUnit unit,
            decimal lowStockThreshold,
            bool tracksExpiry,
            int nearExpiryDays,
            uint version,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var item = await LoadAsync(id, cancellationToken);
            if (item is null)
            {
                return Result.Failure<InventoryItemResponse>(InventoryItemErrors.NotFound());
            }

            if (GetVersion(item) != version)
            {
                return Result.Failure<InventoryItemResponse>(InventoryItemErrors.ConcurrencyConflict());
            }

            var previousName = item.Name;
            var previousUnit = item.Unit;
            var previousThreshold = item.LowStockThreshold;
            var previousTracksExpiry = item.TracksExpiry;
            var previousNearExpiryDays = item.NearExpiryDays;
            if (!item.Update(
                    name,
                    unit,
                    lowStockThreshold,
                    tracksExpiry,
                    nearExpiryDays,
                    timeProvider.GetUtcNow(),
                    actorUserId))
            {
                return Result.Success(ToResponse(item, version));
            }

            context.Entry(item).Property<uint>("xmin").OriginalValue = version;
            var changes = TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange(nameof(InventoryItem.Name), previousName, item.Name),
                    new TenantAuditChanges.FieldChange(
                        nameof(InventoryItem.Unit), previousUnit.ToString(), item.Unit.ToString()),
                    new TenantAuditChanges.FieldChange(
                        nameof(InventoryItem.LowStockThreshold),
                        InventoryItem.FormatQuantity(previousThreshold),
                        InventoryItem.FormatQuantity(item.LowStockThreshold)),
                    new TenantAuditChanges.FieldChange(
                        nameof(InventoryItem.TracksExpiry),
                        previousTracksExpiry.ToString(),
                        item.TracksExpiry.ToString()),
                    new TenantAuditChanges.FieldChange(
                        nameof(InventoryItem.NearExpiryDays),
                        previousNearExpiryDays.ToString(CultureInfo.InvariantCulture),
                        item.NearExpiryDays.ToString(CultureInfo.InvariantCulture)),
                }
                .Where(field => field.HasChanged));

            return await SaveAsync(item, actorUserId, "inventory_items.updated.succeeded", changes, cancellationToken);
        }

        public async Task<Result<InventoryItemResponse>> ChangeStatusAsync(
            Guid id,
            InventoryItemStatus status,
            uint version,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var item = await LoadAsync(id, cancellationToken);
            if (item is null)
            {
                return Result.Failure<InventoryItemResponse>(InventoryItemErrors.NotFound());
            }

            if (GetVersion(item) != version)
            {
                return Result.Failure<InventoryItemResponse>(InventoryItemErrors.ConcurrencyConflict());
            }

            var previousStatus = item.Status;
            var changed = status == InventoryItemStatus.Active
                ? item.Reactivate(timeProvider.GetUtcNow(), actorUserId)
                : item.Deactivate(timeProvider.GetUtcNow(), actorUserId);
            if (!changed)
            {
                return Result.Success(ToResponse(item, version));
            }

            context.Entry(item).Property<uint>("xmin").OriginalValue = version;
            var changes = TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(InventoryItem.Status), previousStatus.ToString(), item.Status.ToString()),
            });
            var action = status == InventoryItemStatus.Active
                ? "inventory_items.reactivated.succeeded"
                : "inventory_items.deactivated.succeeded";

            return await SaveAsync(item, actorUserId, action, changes, cancellationToken);
        }

        private Task<InventoryItem?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
            context.InventoryItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        private async Task<Result<InventoryItemResponse>> SaveAsync(
            InventoryItem item,
            Guid actorUserId,
            string action,
            TenantAuditChanges changes,
            CancellationToken cancellationToken)
        {
            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = action,
                TargetType = TargetType,
                TargetId = item.Id,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<InventoryItemResponse>(audit.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<InventoryItemResponse>(InventoryItemErrors.ConcurrencyConflict());
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: ActiveNameIndex,
            })
            {
                return Result.Failure<InventoryItemResponse>(InventoryItemErrors.NameConflict());
            }

            return Result.Success(ToResponse(item, GetVersion(item)));
        }

        private uint GetVersion(InventoryItem item) =>
            context.Entry(item).Property<uint>("xmin").CurrentValue;

        private static InventoryItemResponse ToResponse(InventoryItem item, uint version) =>
            InventoryItemResponse.From(item, version);
    }
}
