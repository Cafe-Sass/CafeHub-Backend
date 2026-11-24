using System.Data;
using System.Data.Common;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CafeHub.Infrastructure.Billing
{
    internal sealed class ReceiptStore(
        TenantDbContext context,
        ICafeBusinessDateProvider businessDateProvider,
        TimeProvider timeProvider) : IReceiptIssuer
    {
        internal const string AllocateSql =
            "INSERT INTO receipt_number_counters (branch_id, business_date, last_number, updated_at) " +
            "VALUES (@branch_id, @business_date, 1, @updated_at) " +
            "ON CONFLICT (branch_id, business_date) DO UPDATE " +
            "SET last_number = receipt_number_counters.last_number + 1, updated_at = EXCLUDED.updated_at " +
            "RETURNING last_number";

        public async Task<Result> IssueAsync(
            Bill bill,
            IReadOnlyList<Order> acceptedOrders,
            IReadOnlyList<Payment> payments,
            Guid actorId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(bill);
            ArgumentNullException.ThrowIfNull(acceptedOrders);
            ArgumentNullException.ThrowIfNull(payments);

            var existing = await context.Receipts.AsNoTracking()
                .AnyAsync(receipt => receipt.BillId == bill.Id, cancellationToken);
            if (existing)
            {
                return Result.Success();
            }

            var businessDate = await businessDateProvider.GetBusinessDateAsync(cancellationToken);
            var number = await AllocateNumberAsync(bill.BranchId, businessDate, cancellationToken);
            var wifiCodes = await ReadReleasedWifiCodesAsync(acceptedOrders, cancellationToken);
            var receipt = ReceiptComposer.Compose(
                Guid.CreateVersion7(),
                bill,
                acceptedOrders,
                payments,
                Receipt.FormatReceiptNumber(businessDate, number),
                now,
                actorId,
                wifiCodes);

            context.Receipts.Add(receipt);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }
            catch (DbUpdateException exception) when (IsBillReceiptClash(exception))
            {
                DetachAddedReceipt(receipt);
                return Result.Success();
            }
        }

        private async Task<Dictionary<Guid, string>> ReadReleasedWifiCodesAsync(
            IReadOnlyList<Order> acceptedOrders,
            CancellationToken cancellationToken)
        {
            var itemIds = acceptedOrders
                .SelectMany(order => order.Items)
                .Select(item => item.Id)
                .ToArray();
            if (itemIds.Length == 0)
            {
                return [];
            }

            var rows = await context.WifiVouchers.AsNoTracking()
                .Where(voucher => voucher.AssignedOrderItemId != null
                    && itemIds.Contains(voucher.AssignedOrderItemId.Value))
                .Select(voucher => new { ItemId = voucher.AssignedOrderItemId!.Value, voucher.Code })
                .ToListAsync(cancellationToken);

            return rows.ToDictionary(row => row.ItemId, row => row.Code);
        }

        private async Task<int> AllocateNumberAsync(
            Guid branchId,
            DateOnly businessDate,
            CancellationToken cancellationToken)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = AllocateSql;
                command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
                command.Parameters.Add(new NpgsqlParameter("branch_id", NpgsqlTypes.NpgsqlDbType.Uuid) { Value = branchId });
                command.Parameters.Add(new NpgsqlParameter("business_date", NpgsqlTypes.NpgsqlDbType.Date) { Value = businessDate });
                command.Parameters.Add(new NpgsqlParameter("updated_at", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = timeProvider.GetUtcNow() });

                var allocated = await command.ExecuteScalarAsync(cancellationToken);
                return allocated is int number
                    ? number
                    : Convert.ToInt32(allocated, System.Globalization.CultureInfo.InvariantCulture);
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }

        private static bool IsBillReceiptClash(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == "uq_receipts_bill_id";

        private void DetachAddedReceipt(Receipt receipt)
        {
            foreach (var entry in context.ChangeTracker.Entries()
                         .Where(entry => entry.State == EntityState.Added
                             && (ReferenceEquals(entry.Entity, receipt)
                                 || entry.Entity is ReceiptLine line && line.ReceiptId == receipt.Id
                                 || entry.Entity is ReceiptPayment payment && payment.ReceiptId == receipt.Id))
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
