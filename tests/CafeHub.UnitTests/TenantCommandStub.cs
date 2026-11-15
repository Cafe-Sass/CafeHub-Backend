using System.Data;
using System.Data.Common;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CafeHub.UnitTests
{
    internal sealed class TenantCommandStub(
        Func<DbCommand, CommandEventData, DataTable> read,
        Action<DbCommand>? execute = null) : DbCommandInterceptor
    {
        public TenantDbContext CreateContext(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql("Host=192.0.2.1;Database=unit-test;Timeout=1")
            .AddInterceptors([this, new SuppressConnectionOpen(), .. interceptors])
            .Options);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Assert.NotNull(execute);
            execute(command);
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(read(command, eventData).CreateDataReader()));

        public static DataTable Scalars<T>(params T[] values)
        {
            var table = new DataTable();
            table.Columns.Add("value", typeof(T));
            foreach (var value in values)
            {
                table.Rows.Add(value);
            }

            return table;
        }

        public static DataTable EntityRow<T>(DbCommand command, CommandEventData eventData, T entity, uint version = 1) where T : class
        {
            var properties = eventData.Context!.Model.FindEntityType(typeof(T))!.GetProperties()
                .ToDictionary(property => property.GetColumnName());
            var table = new DataTable();
            var values = new List<object?>();
            foreach (var field in command.CommandText.Split('\n')[0]["SELECT ".Length..].Split(", "))
            {
                var column = field[(field.IndexOf('.') + 1)..].Trim().Trim('"');
                var property = properties[column];
                var converter = property.GetTypeMapping().Converter;
                table.Columns.Add(column, converter?.ProviderClrType ?? Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType);
                var value = column == "xmin" ? version : property.PropertyInfo?.GetValue(entity);
                values.Add(converter is null ? value : converter.ConvertToProvider(value));
            }

            table.Rows.Add(values.ToArray());
            return table;
        }

        private sealed class SuppressConnectionOpen : DbConnectionInterceptor
        {
            public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
                DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult.Suppress());
        }
    }
}
