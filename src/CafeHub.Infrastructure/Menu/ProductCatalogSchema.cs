using System.Data;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Menu
{
    internal sealed class ProductCatalogSchema(TenantDbContext context) : IProductCatalogSchema
    {
        public async Task<Result<T>> RunAsync<T>(
            Func<Task<Result<T>>> operation, CancellationToken cancellationToken, bool repeatableRead = false)
        {
            await using var transaction = context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(
                    repeatableRead ? IsolationLevel.RepeatableRead : IsolationLevel.ReadCommitted, cancellationToken)
                : null;

            try
            {
                // A concurrent downgrade must wait until the protected catalog operation has finished.
                await context.Database.ExecuteSqlRawAsync(
                    "LOCK TABLE public.product_tags IN ACCESS SHARE MODE", cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                return Result.Failure<T>(ProductErrors.CatalogSchemaNotReady());
            }

            // The finalized physical key is authoritative and the runtime role cannot change it.
            var finalized = await context.Database.SqlQueryRaw<bool>("""
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_catalog.pg_constraint AS pk
                    JOIN pg_catalog.pg_attribute AS column_info ON column_info.attrelid = pk.conrelid
                    WHERE pk.conrelid = 'public.product_tags'::regclass
                      AND pk.contype = 'p'
                      AND column_info.attname = 'id'
                      AND column_info.atttypid = 'uuid'::regtype
                      AND pk.conkey = ARRAY[column_info.attnum]
                ) AS "Value"
                """).SingleAsync(cancellationToken);
            if (!finalized)
            {
                return Result.Failure<T>(ProductErrors.CatalogSchemaNotReady());
            }

            var result = await operation();
            if (result.IsSuccess && transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
        }
    }
}
