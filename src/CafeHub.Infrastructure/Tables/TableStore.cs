using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Tables
{
    internal sealed class TableStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        ITableQrCodeGenerator qrCodeGenerator,
        TimeProvider timeProvider) : ITableStore
    {
        private const string TargetType = "branch_tables";
        private const string ActiveNumberIndex = "uq_branch_tables_branch_active_number";
        private const string QrCodeIndex = "uq_branch_tables_qr_code";

        public async Task<Result<TableResponse>> AddAsync(
            Guid branchId,
            string? label,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            await context.LockBranchesAsync(cancellationToken, branchId);
            var branch = await context.Branches
                .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);
            if (branch is null)
            {
                return Result.Failure<TableResponse>(TablesErrors.BranchNotFound());
            }

            if (branch.LifecycleStatus != BranchLifecycleStatus.Active)
            {
                return Result.Failure<TableResponse>(TablesErrors.BranchNotActive());
            }

            var maxNumber = await MaxActiveNumberAsync(branchId, cancellationToken);
            var number = maxNumber is null ? BranchTable.MinimumNumber : maxNumber.Value + 1;
            if (number > BranchTable.MaximumNumber)
            {
                return Result.Failure<TableResponse>(TablesErrors.NumberRangeExceeded());
            }

            var qrCode = qrCodeGenerator.Generate();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var table = BranchTable.Create(
                    Guid.CreateVersion7(),
                    branchId,
                    number,
                    label,
                    qrCode,
                    timeProvider.GetUtcNow(),
                    actorUserId);

                context.BranchTables.Add(table);

                var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
                {
                    Action = "tables.created.succeeded",
                    TargetType = TargetType,
                    TargetId = table.Id,
                    ActorUserId = actorUserId,
                    Changes = TenantAuditChangeSets.TableCreate(table),
                }, cancellationToken);
                if (auditResult.IsFailure)
                {
                    return Result.Failure<TableResponse>(auditResult.Error!);
                }

                var saveError = await TrySaveAsync(cancellationToken);
                if (saveError is null)
                {
                    return Result.Success(TableResponse.From(
                        table,
                        ConcurrencyToken(table),
                        table.QrCode));
                }

                if (attempt == 0 && saveError.Code == TablesErrors.QrCodeConflictCode)
                {
                    context.Entry(table).State = EntityState.Detached;
                    qrCode = qrCodeGenerator.Generate();
                    continue;
                }

                return Result.Failure<TableResponse>(saveError);
            }

            return Result.Failure<TableResponse>(TablesErrors.QrCodeConflict());
        }

        public async Task<Result<TableBulkResult>> AddBulkAsync(
            Guid branchId,
            int count,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            await context.LockBranchesAsync(cancellationToken, branchId);
            var branch = await context.Branches
                .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);
            if (branch is null)
            {
                return Result.Failure<TableBulkResult>(TablesErrors.BranchNotFound());
            }

            if (branch.LifecycleStatus != BranchLifecycleStatus.Active)
            {
                return Result.Failure<TableBulkResult>(TablesErrors.BranchNotActive());
            }

            var maxNumber = await MaxActiveNumberAsync(branchId, cancellationToken);
            var startNumber = maxNumber is null ? 0 : maxNumber.Value;
            var fromNumber = startNumber + 1;
            var toNumber = startNumber + count;
            if (toNumber > BranchTable.MaximumNumber)
            {
                return Result.Failure<TableBulkResult>(TablesErrors.NumberRangeExceeded());
            }

            var now = timeProvider.GetUtcNow();
            for (var i = 0; i < count; i++)
            {
                var number = fromNumber + i;
                var table = BranchTable.Create(
                    Guid.CreateVersion7(),
                    branchId,
                    number,
                    null,
                    qrCodeGenerator.Generate(),
                    now,
                    actorUserId);
                context.BranchTables.Add(table);
            }

            var changes = TenantAuditChangeSets.TableBulkCreate(count, fromNumber, toNumber);
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "tables.created.succeeded",
                TargetType = TargetType,
                TargetId = branchId,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<TableBulkResult>(auditResult.Error!);
            }

            var saveError = await TrySaveAsync(cancellationToken);
            if (saveError is not null)
            {
                return Result.Failure<TableBulkResult>(saveError);
            }

            return Result.Success(new TableBulkResult(count, fromNumber, toNumber));
        }

        public async Task<Result<TableResponse>> UpdateLabelAsync(
            Guid tableId,
            Guid? scopeBranchId,
            string? label,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var table = await LoadInScopeAsync(tableId, scopeBranchId, cancellationToken);
            if (table is null)
            {
                return Result.Failure<TableResponse>(TablesErrors.NotFound());
            }

            if (!uint.TryParse(concurrencyToken, out var token))
            {
                return Result.Failure<TableResponse>(TablesErrors.InvalidConcurrencyToken());
            }

            context.Entry(table).Property<uint>("xmin").OriginalValue = token;

            await context.LockBranchesAsync(cancellationToken, table.BranchId);
            var version = context.Entry(table).Property<uint>("xmin");
            if (version.CurrentValue != token)
            {
                return Result.Failure<TableResponse>(TablesErrors.ConcurrencyConflict());
            }

            var trimmedLabel = label?.Trim();
            var changes = TenantAuditChangeSets.TableUpdate(table, trimmedLabel);
            table.UpdateLabel(trimmedLabel, timeProvider.GetUtcNow(), actorUserId);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "tables.updated.succeeded",
                TargetType = TargetType,
                TargetId = table.Id,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<TableResponse>(auditResult.Error!);
            }

            var saveError = await TrySaveAsync(cancellationToken);
            if (saveError is not null)
            {
                return Result.Failure<TableResponse>(saveError);
            }

            return Result.Success(TableResponse.From(table, ConcurrencyToken(table), table.QrCode));
        }

        public async Task<Result<TableResponse>> DeactivateAsync(
            Guid tableId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken) =>
            await ChangeStatusAsync(
                tableId, scopeBranchId, BranchTableStatus.Inactive, "tables.deactivated.succeeded",
                concurrencyToken, actorUserId, cancellationToken);

        public async Task<Result<TableResponse>> ReactivateAsync(
            Guid tableId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken) =>
            await ChangeStatusAsync(
                tableId, scopeBranchId, BranchTableStatus.Active, "tables.reactivated.succeeded",
                concurrencyToken, actorUserId, cancellationToken);

        public async Task<Result<TableBulkResult>> DeactivateHighestAsync(
            Guid branchId,
            int count,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var activeCount = await CountActiveAsync(branchId, cancellationToken);
            if (count < 1 || count > activeCount)
            {
                return Result.Failure<TableBulkResult>(TablesErrors.BulkCountUnavailable());
            }

            await context.LockBranchesAsync(cancellationToken, branchId);
            var branch = await context.Branches
                .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);
            if (branch is null)
            {
                return Result.Failure<TableBulkResult>(TablesErrors.BranchNotFound());
            }

            if (branch.LifecycleStatus != BranchLifecycleStatus.Active)
            {
                return Result.Failure<TableBulkResult>(TablesErrors.BranchNotActive());
            }

            var now = timeProvider.GetUtcNow();
            var tables = await context.BranchTables
                .Where(table => table.BranchId == branchId && table.Status == BranchTableStatus.Active)
                .OrderByDescending(table => table.Number)
                .Take(count)
                .ToListAsync(cancellationToken);

            var fromNumber = tables[tables.Count - 1].Number;
            var toNumber = tables[0].Number;
            foreach (var table in tables)
            {
                table.Deactivate(now, actorUserId);
            }

            var changes = TenantAuditChangeSets.TableBulkDeactivate(count, fromNumber, toNumber);
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "tables.deactivated.succeeded",
                TargetType = TargetType,
                TargetId = branchId,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<TableBulkResult>(auditResult.Error!);
            }

            var saveError = await TrySaveAsync(cancellationToken);
            if (saveError is not null)
            {
                return Result.Failure<TableBulkResult>(saveError);
            }

            return Result.Success(new TableBulkResult(count, fromNumber, toNumber));
        }

        public Task<TableResponse?> GetByIdAsync(
            Guid tableId,
            Guid? scopeBranchId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TableResponse?>(null);

        public async Task<TableListPage> ListAsync(
            TableListRequest request,
            CancellationToken cancellationToken)
        {
            var query = context.BranchTables.Where(table => table.BranchId == request.BranchId);
            if (request.Status is { } status)
            {
                query = query.Where(table => table.Status == status);
            }

            if (request.FromNumber is { } fromNumber)
            {
                query = query.Where(table => table.Number >= fromNumber);
            }

            if (request.ToNumber is { } toNumber)
            {
                query = query.Where(table => table.Number <= toNumber);
            }

            int? totalCount = null;
            if (request.IncludeTotalCount)
            {
                totalCount = await query.CountAsync(cancellationToken);
            }

            var fetched = await query
                .OrderBy(table => table.Number)
                .ThenBy(table => table.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            var items = fetched.Select(table =>
                TableResponse.From(
                    table,
                    ConcurrencyToken(table),
                    table.QrCode)).ToList();

            return new TableListPage(items, totalCount);
        }

        public Task<int> CountActiveAsync(Guid branchId, CancellationToken cancellationToken) =>
            context.BranchTables.CountAsync(
                table => table.BranchId == branchId && table.Status == BranchTableStatus.Active,
                cancellationToken);

        public Task<TableQrData?> GetQrAsync(Guid branchId, Guid tableId, CancellationToken cancellationToken) =>
            ProjectQrData(context.BranchTables.AsNoTracking()
                .Where(table => table.BranchId == branchId && table.Id == tableId))
                .SingleOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<TableQrData>?> GetQrSheetAsync(Guid branchId, CancellationToken cancellationToken)
        {
            if (!await context.Branches.AnyAsync(branch => branch.Id == branchId, cancellationToken))
            {
                return null;
            }

            return await ProjectQrData(context.BranchTables.AsNoTracking()
                .Where(table => table.BranchId == branchId && table.Status == BranchTableStatus.Active)
                .OrderBy(table => table.Number))
                .ToListAsync(cancellationToken);
        }

        public async Task<Result<TableQrData>> RegenerateQrAsync(
            Guid branchId,
            Guid tableId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            await context.LockBranchesAsync(cancellationToken, branchId);
            var table = await LoadInScopeAsync(tableId, branchId, cancellationToken);
            if (table is null)
            {
                return Result.Failure<TableQrData>(TablesErrors.NotFound());
            }

            if (table.Status != BranchTableStatus.Active)
            {
                return Result.Failure<TableQrData>(TablesErrors.TableNotActive());
            }

            if (!uint.TryParse(concurrencyToken, NumberStyles.None, CultureInfo.InvariantCulture, out var token))
            {
                return Result.Failure<TableQrData>(TablesErrors.InvalidConcurrencyToken());
            }

            var version = context.Entry(table).Property<uint>("xmin");
            if (version.CurrentValue != token)
            {
                return Result.Failure<TableQrData>(TablesErrors.ConcurrencyConflict());
            }

            var replacementCode = qrCodeGenerator.Generate();
            if (string.Equals(table.QrCode, replacementCode, StringComparison.Ordinal))
            {
                return Result.Failure<TableQrData>(TablesErrors.QrCodeConflict());
            }

            table.RegenerateQrCode(replacementCode, timeProvider.GetUtcNow(), actorUserId);
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "tables.qr_regenerated.succeeded",
                TargetType = TargetType,
                TargetId = table.Id,
                ActorUserId = actorUserId,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<TableQrData>(auditResult.Error!);
            }

            var saveError = await TrySaveAsync(cancellationToken);
            return saveError is null
                ? Result.Success(new TableQrData(table.Id, table.Number, table.Label, table.QrCode, version.CurrentValue))
                : Result.Failure<TableQrData>(saveError);
        }

        public Task<GuestQrContext?> ResolveGuestQrAsync(string code, CancellationToken cancellationToken) =>
            (from table in context.BranchTables.AsNoTracking()
             join branch in context.Branches on table.BranchId equals branch.Id
             from settings in context.CafeSettings
             where table.QrCode == code
                 && table.Status == BranchTableStatus.Active
                 && branch.LifecycleStatus == BranchLifecycleStatus.Active
             select new GuestQrContext(settings.CafeName, branch.Id, branch.Name, table.Id, table.Number))
            .SingleOrDefaultAsync(cancellationToken);

        private static IQueryable<TableQrData> ProjectQrData(IQueryable<BranchTable> tables) =>
            tables.Select(table => new TableQrData(
                table.Id, table.Number, table.Label, table.QrCode, EF.Property<uint>(table, "xmin")));

        public Task<int?> MaxActiveNumberAsync(Guid branchId, CancellationToken cancellationToken) =>
            context.BranchTables
                .Where(table => table.BranchId == branchId && table.Status == BranchTableStatus.Active)
                .MaxAsync(table => (int?)table.Number, cancellationToken);

        private async Task<Result<TableResponse>> ChangeStatusAsync(
            Guid tableId,
            Guid? scopeBranchId,
            BranchTableStatus newStatus,
            string auditAction,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var table = await LoadInScopeAsync(tableId, scopeBranchId, cancellationToken);
            if (table is null)
            {
                return Result.Failure<TableResponse>(TablesErrors.NotFound());
            }

            if (!uint.TryParse(concurrencyToken, out var token))
            {
                return Result.Failure<TableResponse>(TablesErrors.InvalidConcurrencyToken());
            }

            await context.LockBranchesAsync(cancellationToken, table.BranchId);

            var version = context.Entry(table).Property<uint>("xmin");
            if (version.CurrentValue != token)
            {
                return Result.Failure<TableResponse>(TablesErrors.ConcurrencyConflict());
            }

            if (table.Status == newStatus)
            {
                return Result.Success(TableResponse.From(
                    table,
                    version.CurrentValue.ToString(CultureInfo.InvariantCulture),
                    table.QrCode));
            }

            if (newStatus == BranchTableStatus.Active
                && await context.BranchTables.AnyAsync(
                    other => other.BranchId == table.BranchId
                        && other.Status == BranchTableStatus.Active
                        && other.Number == table.Number
                        && other.Id != table.Id,
                    cancellationToken))
            {
                return Result.Failure<TableResponse>(TablesErrors.NumberConflict());
            }

            version.OriginalValue = token;
            var changes = TenantAuditChangeSets.TableLifecycleStatus(table, newStatus);

            if (newStatus == BranchTableStatus.Active)
            {
                table.Reactivate(timeProvider.GetUtcNow(), actorUserId);
            }
            else
            {
                table.Deactivate(timeProvider.GetUtcNow(), actorUserId);
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = auditAction,
                TargetType = TargetType,
                TargetId = table.Id,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<TableResponse>(auditResult.Error!);
            }

            var saveError = await TrySaveAsync(cancellationToken);
            if (saveError is not null)
            {
                return Result.Failure<TableResponse>(saveError);
            }

            return Result.Success(TableResponse.From(table, ConcurrencyToken(table), table.QrCode));
        }

        private async Task<BranchTable?> LoadInScopeAsync(
            Guid tableId,
            Guid? scopeBranchId,
            CancellationToken cancellationToken) =>
            await context.BranchTables.FirstOrDefaultAsync(
                table => table.Id == tableId
                    && (scopeBranchId == null || table.BranchId == scopeBranchId),
                cancellationToken);

        private async Task<Error?> TrySaveAsync(CancellationToken cancellationToken)
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return null;
            }
            catch (DbUpdateConcurrencyException)
            {
                return TablesErrors.ConcurrencyConflict();
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            } violation && violation.ConstraintName is ActiveNumberIndex or QrCodeIndex)
            {
                return violation.ConstraintName == ActiveNumberIndex
                    ? TablesErrors.NumberConflict()
                    : TablesErrors.QrCodeConflict();
            }
        }

        private string ConcurrencyToken(BranchTable table) =>
            context.Entry(table).Property<uint>("xmin").CurrentValue.ToString(CultureInfo.InvariantCulture);
    }
}
