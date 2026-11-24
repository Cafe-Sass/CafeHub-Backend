using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Wifi;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CafeHub.Infrastructure.Wifi
{
    internal sealed class WifiVoucherStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IWifiVoucherStore
    {
        private const string TargetType = "wifi_vouchers";
        internal const string ClaimSql =
            "UPDATE wifi_vouchers SET status = 'Assigned', assigned_order_item_id = @order_item_id, " +
            "assigned_at = @now, updated_at = @now WHERE id = (" +
            "SELECT id FROM wifi_vouchers WHERE package_id = @package_id AND status = 'Available' " +
            "ORDER BY imported_at, id LIMIT 1 FOR UPDATE SKIP LOCKED) RETURNING id";

        public async Task<IReadOnlySet<string>> FindExistingCodesAsync(
            Guid packageId,
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken)
        {
            if (codes.Count == 0)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var candidates = codes.ToList();
            var existing = await context.WifiVouchers.AsNoTracking()
                .Where(voucher => voucher.PackageId == packageId && candidates.Contains(voucher.Code))
                .Select(voucher => voucher.Code)
                .ToListAsync(cancellationToken);

            return existing.ToHashSet(StringComparer.Ordinal);
        }

        public Task<bool> HasAvailableAsync(Guid packageId, CancellationToken cancellationToken) =>
            context.WifiVouchers.AsNoTracking()
                .AnyAsync(
                    voucher => voucher.PackageId == packageId
                        && voucher.Status == WifiVoucherStatus.Available,
                    cancellationToken);

        public async Task<Result> IssueForOrderAsync(
            Order order,
            Guid? actorUserId,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(order);

            foreach (var item in order.Items)
            {
                if (item.WifiPackageId is not { } packageId)
                {
                    continue;
                }

                var alreadyAssigned = await context.WifiVouchers.AsNoTracking()
                    .AnyAsync(voucher => voucher.AssignedOrderItemId == item.Id, cancellationToken);
                if (alreadyAssigned)
                {
                    continue;
                }

                var claimed = await ClaimAsync(
                    item.Id,
                    packageId,
                    order.Id,
                    actorUserId,
                    timeProvider.GetUtcNow(),
                    cancellationToken);
                if (!claimed)
                {
                    continue;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        private async Task<bool> ClaimAsync(
            Guid orderItemId,
            Guid packageId,
            Guid orderId,
            Guid? actorUserId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            Guid voucherId;
            try
            {
                await using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = ClaimSql;
                command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
                command.Parameters.Add(new NpgsqlParameter("order_item_id", NpgsqlTypes.NpgsqlDbType.Uuid) { Value = orderItemId });
                command.Parameters.Add(new NpgsqlParameter("package_id", NpgsqlTypes.NpgsqlDbType.Uuid) { Value = packageId });
                command.Parameters.Add(new NpgsqlParameter("now", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = now });

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return false;
                }

                voucherId = reader.GetGuid(0);
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "wifi_vouchers.issued.succeeded",
                TargetType = TargetType,
                TargetId = voucherId,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Create(new[]
                {
                    new TenantAuditChanges.FieldChange("PackageId", null, packageId.ToString()),
                    new TenantAuditChanges.FieldChange("OrderId", null, orderId.ToString()),
                }),
            }, cancellationToken);
            return auditResult.IsSuccess;
        }

        public async Task<Result> InsertImportedAsync(
            Guid packageId,
            IReadOnlyList<WifiVoucher> vouchers,
            WifiVoucherImportResult counts,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            if (vouchers.Count > 0)
            {
                context.WifiVouchers.AddRange(vouchers);
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "wifi_vouchers.imported.succeeded",
                TargetType = TargetType,
                TargetId = packageId,
                ActorUserId = actorUserId,
                Changes = TenantAuditChangeSets.WifiVoucherImport(counts),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }
            catch (DbUpdateException exception) when (IsCodeClash(exception))
            {
                return Result.Failure(WifiErrors.VoucherImportConflict());
            }
        }

        public async Task<WifiVoucherSummary> GetSummaryAsync(
            Guid packageId,
            CancellationToken cancellationToken)
        {
            var counts = await context.WifiVouchers.AsNoTracking()
                .Where(voucher => voucher.PackageId == packageId)
                .GroupBy(voucher => voucher.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);

            var available = counts.FirstOrDefault(row => row.Status == WifiVoucherStatus.Available)?.Count ?? 0;
            var assigned = counts.FirstOrDefault(row => row.Status == WifiVoucherStatus.Assigned)?.Count ?? 0;
            var voided = counts.FirstOrDefault(row => row.Status == WifiVoucherStatus.Void)?.Count ?? 0;
            return new WifiVoucherSummary(available, assigned, voided, available + assigned + voided);
        }

        public async Task<Result> VoidAsync(
            Guid packageId,
            string code,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var voucher = await context.WifiVouchers
                .FirstOrDefaultAsync(
                    candidate => candidate.PackageId == packageId && candidate.Code == code,
                    cancellationToken);
            if (voucher is null)
            {
                return Result.Failure(WifiErrors.VoucherNotFound());
            }

            if (voucher.Status != WifiVoucherStatus.Available)
            {
                return Result.Failure(WifiErrors.VoucherNotAvailable());
            }

            voucher.Void(timeProvider.GetUtcNow());
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "wifi_vouchers.voided.succeeded",
                TargetType = TargetType,
                TargetId = packageId,
                ActorUserId = actorUserId,
                Changes = TenantAuditChangeSets.WifiVoucherVoid(),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure(auditResult.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        private static bool IsCodeClash(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == "uq_wifi_vouchers_package_id_code";
    }
}
