using System.Data;
using System.Data.Common;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class OrderNumberCounterStore(TenantDbContext context, TimeProvider timeProvider)
        : IOrderNumberCounterStore
    {
        internal const string AllocateSql =
            "INSERT INTO order_number_counters (branch_id, business_date, last_number, updated_at) " +
            "VALUES (@branch_id, @business_date, 1, @updated_at) " +
            "ON CONFLICT (branch_id, business_date) DO UPDATE " +
            "SET last_number = order_number_counters.last_number + 1, updated_at = EXCLUDED.updated_at " +
            "RETURNING last_number";

        public async Task<int> IncrementAsync(
            Guid branchId, DateOnly businessDate, CancellationToken cancellationToken)
        {
            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            await context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = AllocateSql;
                command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
                AddParameter(command, "branch_id", NpgsqlDbType.Uuid, branchId);
                AddParameter(command, "business_date", NpgsqlDbType.Date, businessDate);
                AddParameter(command, "updated_at", NpgsqlDbType.TimestampTz, timeProvider.GetUtcNow());

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

        private static void AddParameter(DbCommand command, string name, NpgsqlDbType type, object value)
        {
            var parameter = new NpgsqlParameter(name, type) { Value = value };
            command.Parameters.Add(parameter);
        }
    }
}
