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
    internal sealed class InventoryBatchStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IInventoryBatchStore
    {
        private const string TargetType = "inventory_batches";
        private const string IdempotencyIndex = "uq_inventory_batches_branch_idempotency_key";
        private const string MovementIdempotencyIndex = "uq_stock_movements_branch_key_batch";
        private const string SavepointSql = "SAVEPOINT inventory_batch_insert";
        private const string ReleaseSavepointSql = "RELEASE SAVEPOINT inventory_batch_insert";
        private const string RollbackSavepointSql = "ROLLBACK TO SAVEPOINT inventory_batch_insert";

        public async Task<Result<InventoryBatchResponse>> ReceiveAsync(
            Guid branchId,
            Guid itemId,
            decimal receivedQuantity,
            DateOnly receivedDate,
            DateOnly? expiryDate,
            string idempotencyKey,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var existing = await FindByKeyAsync(branchId, idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return Replay(existing, itemId, receivedQuantity, receivedDate, expiryDate);
            }

            var branch = await context.Branches.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == branchId, cancellationToken);
            if (branch is null || branch.LifecycleStatus != BranchLifecycleStatus.Active)
            {
                return Result.Failure<InventoryBatchResponse>(InventoryBatchErrors.BranchNotFound());
            }

            var item = await context.InventoryItems.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);
            if (item is null)
            {
                return Result.Failure<InventoryBatchResponse>(InventoryItemErrors.NotFound());
            }

            if (item.Status != InventoryItemStatus.Active)
            {
                return Result.Failure<InventoryBatchResponse>(InventoryBatchErrors.ItemInactive());
            }

            if (item.TracksExpiry && expiryDate is null)
            {
                return Result.Failure<InventoryBatchResponse>(InventoryBatchErrors.ExpiryRequired());
            }

            if (!item.TracksExpiry && expiryDate is not null)
            {
                return Result.Failure<InventoryBatchResponse>(InventoryBatchErrors.ExpiryNotTracked());
            }

            if (expiryDate is { } expiry && expiry < receivedDate)
            {
                return Result.Failure<InventoryBatchResponse>(InventoryBatchErrors.ExpiryBeforeReceived());
            }

            var now = timeProvider.GetUtcNow();
            var batch = InventoryBatch.Create(
                Guid.CreateVersion7(),
                itemId,
                branchId,
                receivedQuantity,
                receivedDate,
                expiryDate,
                item.TracksExpiry,
                actorUserId,
                idempotencyKey,
                now,
                actorUserId);
            context.InventoryBatches.Add(batch);

            var movement = StockMovement.Create(
                Guid.CreateVersion7(),
                itemId,
                branchId,
                batch.Id,
                StockMovementType.Receive,
                receivedQuantity,
                null,
                idempotencyKey,
                now,
                actorUserId);
            context.StockMovements.Add(movement);

            var changes = TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(InventoryBatch.ItemId), null, batch.ItemId.ToString()),
                new TenantAuditChanges.FieldChange(
                    nameof(InventoryBatch.BranchId), null, batch.BranchId.ToString()),
                new TenantAuditChanges.FieldChange(
                    nameof(InventoryBatch.ReceivedQuantity),
                    null,
                    InventoryItem.FormatQuantity(batch.ReceivedQuantity)),
                new TenantAuditChanges.FieldChange(
                    nameof(InventoryBatch.RemainingQuantity),
                    null,
                    InventoryItem.FormatQuantity(batch.RemainingQuantity)),
                new TenantAuditChanges.FieldChange(
                    nameof(InventoryBatch.ReceivedDate), null, InventoryBatch.FormatDate(batch.ReceivedDate)),
                new TenantAuditChanges.FieldChange(
                    nameof(InventoryBatch.ExpiryDate),
                    null,
                    batch.ExpiryDate is { } date ? InventoryBatch.FormatDate(date) : null),
            });

            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "inventory_batches.received.succeeded",
                TargetType = TargetType,
                TargetId = batch.Id,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<InventoryBatchResponse>(audit.Error!);
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
            } postgres
                && postgres.ConstraintName is IdempotencyIndex or MovementIdempotencyIndex)
            {
                context.Entry(batch).State = EntityState.Detached;
                context.Entry(movement).State = EntityState.Detached;
                foreach (var entry in context.ChangeTracker.Entries<TenantAuditRecord>()
                    .Where(candidate => candidate.State == EntityState.Added)
                    .ToArray())
                {
                    entry.State = EntityState.Detached;
                }

                if (usesSavepoint)
                {
                    await context.Database.ExecuteSqlRawAsync(RollbackSavepointSql, cancellationToken);
                }

                var stored = await FindByKeyAsync(branchId, idempotencyKey, cancellationToken);
                return stored is null
                    ? Result.Failure<InventoryBatchResponse>(IdempotencyErrors.KeyReused())
                    : Replay(stored, itemId, receivedQuantity, receivedDate, expiryDate);
            }

            if (usesSavepoint)
            {
                await context.Database.ExecuteSqlRawAsync(ReleaseSavepointSql, cancellationToken);
            }

            return Result.Success(InventoryBatchResponse.From(batch));
        }

        public async Task<Result<InventoryBatchListPage>> ListAsync(
            InventoryBatchListRequest request, CancellationToken cancellationToken)
        {
            var branchExists = await context.Branches.AsNoTracking()
                .AnyAsync(branch => branch.Id == request.BranchId, cancellationToken);
            if (!branchExists)
            {
                return Result.Failure<InventoryBatchListPage>(InventoryBatchErrors.BranchNotFound());
            }

            var query = context.InventoryBatches.AsNoTracking()
                .Where(batch => batch.BranchId == request.BranchId);
            if (request.ItemId is { } itemId)
            {
                query = query.Where(batch => batch.ItemId == itemId);
            }

            if (request.ExpiryFrom is { } from)
            {
                query = query.Where(batch => batch.ExpiryDate != null && batch.ExpiryDate >= from);
            }

            if (request.ExpiryTo is { } to)
            {
                query = query.Where(batch => batch.ExpiryDate != null && batch.ExpiryDate <= to);
            }

            int? totalCount = null;
            if (request.IncludeTotalCount)
            {
                totalCount = await query.CountAsync(cancellationToken);
            }

            var fetched = await query
                .OrderByDescending(batch => batch.ReceivedDate)
                .ThenByDescending(batch => batch.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            var items = new List<InventoryBatchResponse>(fetched.Count);
            foreach (var batch in fetched)
            {
                items.Add(InventoryBatchResponse.From(batch));
            }

            return Result.Success(new InventoryBatchListPage(items, totalCount));
        }

        public async Task<InventoryStockLevelPage> ListStockLevelsAsync(
            InventoryStockLevelRequest request, CancellationToken cancellationToken)
        {
            var query = context.InventoryBatches.AsNoTracking();
            if (request.BranchId is { } branchId)
            {
                query = query.Where(batch => batch.BranchId == branchId);
            }

            if (request.ItemId is { } itemId)
            {
                query = query.Where(batch => batch.ItemId == itemId);
            }

            var grouped = query
                .GroupBy(batch => new { batch.ItemId, batch.BranchId })
                .Select(group => new
                {
                    group.Key.ItemId,
                    group.Key.BranchId,
                    RemainingQuantity = group.Sum(batch => batch.RemainingQuantity),
                    NearestExpiryDate = group.Min(batch =>
                        batch.RemainingQuantity > 0m ? batch.ExpiryDate : null),
                });

            int? totalCount = null;
            if (request.IncludeTotalCount)
            {
                totalCount = await grouped.CountAsync(cancellationToken);
            }

            var fetched = await grouped
                .OrderBy(row => row.ItemId)
                .ThenBy(row => row.BranchId)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            var items = fetched
                .Select(row => new StockLevelResponse(
                    row.ItemId, row.BranchId, row.RemainingQuantity, row.NearestExpiryDate))
                .ToList();

            return new InventoryStockLevelPage(items, totalCount);
        }

        private Task<InventoryBatch?> FindByKeyAsync(
            Guid branchId, string idempotencyKey, CancellationToken cancellationToken) =>
            context.InventoryBatches.AsNoTracking()
                .FirstOrDefaultAsync(
                    batch => batch.BranchId == branchId && batch.IdempotencyKey == idempotencyKey,
                    cancellationToken);

        private static Result<InventoryBatchResponse> Replay(
            InventoryBatch batch,
            Guid itemId,
            decimal receivedQuantity,
            DateOnly receivedDate,
            DateOnly? expiryDate)
        {
            var matches = batch.ItemId == itemId
                && batch.ReceivedQuantity == receivedQuantity
                && batch.ReceivedDate == receivedDate
                && batch.ExpiryDate == expiryDate;

            return matches
                ? Result.Success(InventoryBatchResponse.From(batch))
                : Result.Failure<InventoryBatchResponse>(IdempotencyErrors.KeyReused());
        }
    }
}
