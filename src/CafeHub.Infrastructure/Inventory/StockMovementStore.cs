using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Inventory;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Inventory
{
    internal sealed class StockMovementStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IStockMovementStore
    {
        private const string TargetType = "stock_movements";
        private const string IdempotencyIndex = "uq_stock_movements_branch_key_batch";
        private const string SavepointSql = "SAVEPOINT stock_movement_insert";
        private const string ReleaseSavepointSql = "RELEASE SAVEPOINT stock_movement_insert";
        private const string RollbackSavepointSql = "ROLLBACK TO SAVEPOINT stock_movement_insert";

        public async Task<Result<IReadOnlyList<StockMovementResponse>>> RecordAsync(
            Guid branchId,
            Guid itemId,
            Guid? batchId,
            StockMovementType type,
            decimal quantity,
            string reason,
            string idempotencyKey,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var existing = await LoadByKeyAsync(branchId, idempotencyKey, cancellationToken);
            if (existing.Count > 0)
            {
                return Replay(existing, itemId, type, reason, quantity, batchId);
            }

            if (!StockMovement.IsManualType(type))
            {
                return Result.Failure<IReadOnlyList<StockMovementResponse>>(StockMovementErrors.InvalidType());
            }

            if (!StockMovement.IsValidReason(reason) || string.IsNullOrWhiteSpace(reason))
            {
                return Result.Failure<IReadOnlyList<StockMovementResponse>>(StockMovementErrors.ReasonRequired());
            }

            var branch = await context.Branches.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == branchId, cancellationToken);
            if (branch is null || branch.LifecycleStatus != BranchLifecycleStatus.Active)
            {
                return Result.Failure<IReadOnlyList<StockMovementResponse>>(InventoryBatchErrors.BranchNotFound());
            }

            var item = await context.InventoryItems.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);
            if (item is null)
            {
                return Result.Failure<IReadOnlyList<StockMovementResponse>>(InventoryItemErrors.NotFound());
            }

            if (item.Status != InventoryItemStatus.Active)
            {
                return Result.Failure<IReadOnlyList<StockMovementResponse>>(InventoryBatchErrors.ItemInactive());
            }

            var now = timeProvider.GetUtcNow();
            var movements = new List<StockMovement>();
            if (type == StockMovementType.Adjust)
            {
                var adjusted = await ApplyAdjustmentAsync(
                    branchId, itemId, batchId, quantity, reason, idempotencyKey, now, actorUserId, cancellationToken);
                if (adjusted.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<StockMovementResponse>>(adjusted.Error!);
                }

                movements.Add(adjusted.Value);
            }
            else
            {
                var allocated = await ApplyAllocationAsync(
                    branchId,
                    itemId,
                    type,
                    quantity,
                    reason,
                    idempotencyKey,
                    now,
                    actorUserId,
                    cancellationToken);
                if (allocated.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<StockMovementResponse>>(allocated.Error!);
                }

                movements.AddRange(allocated.Value);
            }

            context.StockMovements.AddRange(movements);
            foreach (var movement in movements)
            {
                var audit = await auditWriter.WriteAsync(
                    new TenantAuditEntry
                    {
                        Action = $"stock_movements.{movement.Type.ToString().ToLowerInvariant()}.succeeded",
                        TargetType = TargetType,
                        TargetId = movement.Id,
                        ActorUserId = actorUserId,
                        Changes = TenantAuditChanges.Create(new[]
                        {
                            new TenantAuditChanges.FieldChange(
                                nameof(StockMovement.ItemId), null, movement.ItemId.ToString()),
                            new TenantAuditChanges.FieldChange(
                                nameof(StockMovement.BranchId), null, movement.BranchId.ToString()),
                            new TenantAuditChanges.FieldChange(
                                nameof(StockMovement.BatchId), null, movement.BatchId.ToString()),
                            new TenantAuditChanges.FieldChange(
                                nameof(StockMovement.Type), null, movement.Type.ToString()),
                            new TenantAuditChanges.FieldChange(
                                nameof(StockMovement.Quantity),
                                null,
                                StockMovement.FormatQuantity(movement.Quantity)),
                            new TenantAuditChanges.FieldChange(
                                nameof(StockMovement.Reason), null, movement.Reason),
                        }),
                    },
                    cancellationToken);
                if (audit.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<StockMovementResponse>>(audit.Error!);
                }
            }

            var usesSavepoint = context.Database.CurrentTransaction is not null;
            if (usesSavepoint)
            {
                await context.Database.ExecuteSqlRawAsync(SavepointSql, cancellationToken);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: IdempotencyIndex,
            })
            {
                if (usesSavepoint)
                {
                    await context.Database.ExecuteSqlRawAsync(RollbackSavepointSql, cancellationToken);
                }

                context.ChangeTracker.Clear();
                var stored = await LoadByKeyAsync(branchId, idempotencyKey, cancellationToken);
                return stored.Count == 0
                    ? Result.Failure<IReadOnlyList<StockMovementResponse>>(IdempotencyErrors.KeyReused())
                    : Replay(stored, itemId, type, reason, quantity, batchId);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<IReadOnlyList<StockMovementResponse>>(
                    StockMovementErrors.ConcurrencyConflict());
            }

            if (usesSavepoint)
            {
                await context.Database.ExecuteSqlRawAsync(ReleaseSavepointSql, cancellationToken);
            }

            return Result.Success<IReadOnlyList<StockMovementResponse>>(
                movements.Select(StockMovementResponse.From).ToList());
        }

        private async Task<Result<StockMovement>> ApplyAdjustmentAsync(
            Guid branchId,
            Guid itemId,
            Guid? batchId,
            decimal quantity,
            string reason,
            string idempotencyKey,
            DateTimeOffset now,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            if (batchId is not { } selectedBatchId)
            {
                return Result.Failure<StockMovement>(StockMovementErrors.BatchRequired());
            }

            var batch = await context.InventoryBatches.FirstOrDefaultAsync(
                candidate => candidate.Id == selectedBatchId
                    && candidate.ItemId == itemId
                    && candidate.BranchId == branchId,
                cancellationToken);
            if (batch is null)
            {
                return Result.Failure<StockMovement>(StockMovementErrors.BatchNotFound());
            }

            if (!batch.CanApplyMovement(quantity))
            {
                return Result.Failure<StockMovement>(
                    quantity < 0m
                        ? StockMovementErrors.InsufficientBatchStock()
                        : StockMovementErrors.AdjustmentExceedsReceived());
            }

            batch.ApplyMovement(quantity, now, actorUserId);
            return Result.Success(CreateMovement(
                batch.Id, itemId, branchId, StockMovementType.Adjust,
                quantity, reason, idempotencyKey, now, actorUserId));
        }

        private async Task<Result<IReadOnlyList<StockMovement>>> ApplyAllocationAsync(
            Guid branchId,
            Guid itemId,
            StockMovementType type,
            decimal quantity,
            string reason,
            string idempotencyKey,
            DateTimeOffset now,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var batches = await context.InventoryBatches
                .Where(batch => batch.BranchId == branchId
                    && batch.ItemId == itemId
                    && batch.RemainingQuantity > 0m)
                .ToListAsync(cancellationToken);

            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (!StockAllocation.TryAllocate(
                    batches,
                    quantity,
                    today,
                    allowExpired: type == StockMovementType.Waste,
                    out var allocations))
            {
                return Result.Failure<IReadOnlyList<StockMovement>>(StockMovementErrors.InsufficientStock());
            }

            var movements = new List<StockMovement>(allocations.Count);
            foreach (var allocation in allocations)
            {
                var batch = batches.Single(candidate => candidate.Id == allocation.BatchId);
                var signedQuantity = -allocation.Quantity;
                batch.ApplyMovement(signedQuantity, now, actorUserId);
                movements.Add(CreateMovement(
                    batch.Id, itemId, branchId, type, signedQuantity, reason, idempotencyKey, now, actorUserId));
            }

            return Result.Success<IReadOnlyList<StockMovement>>(movements);
        }

        private static StockMovement CreateMovement(
            Guid batchId,
            Guid itemId,
            Guid branchId,
            StockMovementType type,
            decimal quantity,
            string reason,
            string idempotencyKey,
            DateTimeOffset now,
            Guid actorUserId) =>
            StockMovement.Create(
                Guid.CreateVersion7(),
                itemId,
                branchId,
                batchId,
                type,
                quantity,
                reason,
                idempotencyKey,
                now,
                actorUserId);

        private Task<List<StockMovement>> LoadByKeyAsync(
            Guid branchId, string idempotencyKey, CancellationToken cancellationToken) =>
            context.StockMovements.AsNoTracking()
                .Where(movement => movement.BranchId == branchId
                    && movement.IdempotencyKey == idempotencyKey)
                .OrderBy(movement => movement.Id)
                .ToListAsync(cancellationToken);

        private static Result<IReadOnlyList<StockMovementResponse>> Replay(
            IReadOnlyList<StockMovement> stored,
            Guid itemId,
            StockMovementType type,
            string reason,
            decimal quantity,
            Guid? batchId)
        {
            return StockMovementReplay.Matches(stored, itemId, type, reason, quantity, batchId)
                ? Result.Success<IReadOnlyList<StockMovementResponse>>(
                    stored.Select(StockMovementResponse.From).ToList())
                : Result.Failure<IReadOnlyList<StockMovementResponse>>(IdempotencyErrors.KeyReused());
        }
    }
}
