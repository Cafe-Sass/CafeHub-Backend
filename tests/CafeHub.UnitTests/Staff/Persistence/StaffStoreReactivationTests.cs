using System.Data;
using System.Data.Common;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Staff;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.Infrastructure.Staff;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CafeHub.UnitTests.Staff.Persistence
{
    public sealed class StaffStoreReactivationTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Reactivate_ChecksTheBranchOnlyForADeactivatedAccount(bool deactivated)
        {
            var actorId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var staff = TenantUser.CreateStaff(
                Guid.NewGuid(), "staff", "staff", "Staff", "staff@example.test", "staff@example.test",
                null, "test-hash", "test-stamp", "test-concurrency-stamp", Guid.NewGuid(), now);
            if (deactivated)
            {
                staff.Deactivate(now, actorId);
            }

            var commands = new ReactivationCommands(staff);
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=192.0.2.1;Database=unit-test;Timeout=1")
                .AddInterceptors(commands, new SuppressConnectionOpen())
                .Options;
            await using var context = new TenantDbContext(options);
            var store = new StaffStore(context, new PasswordHasher<TenantUser>(), new UnexpectedAuditWriter(), TimeProvider.System, new FakeTask42Access());

            var result = await store.ReactivateAsync(staff.Id, actorId, default);

            Assert.Equal(deactivated, commands.BranchChecked);
            Assert.Equal(!deactivated, result.IsSuccess);
            if (deactivated)
            {
                Assert.Equal(StaffErrors.ReactivationBranchNotActive(), result.Error);
            }
        }

        private sealed class ReactivationCommands(TenantUser staff) : DbCommandInterceptor
        {
            public bool BranchChecked { get; private set; }

            public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
                DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal);
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
            }

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                var rows = new DataTable();
                if (command.CommandText.Contains("FROM branches", StringComparison.Ordinal))
                {
                    BranchChecked = true;
                    rows.Columns.Add("exists", typeof(bool));
                    rows.Rows.Add(false);
                }
                else
                {
                    var properties = eventData.Context!.Model.FindEntityType(typeof(TenantUser))!.GetProperties()
                        .ToDictionary(property => property.GetColumnName());
                    var projection = command.CommandText.Split('\n')[0]["SELECT ".Length..].Split(", ");
                    var values = new List<object?>();
                    foreach (var field in projection)
                    {
                        var column = field[(field.IndexOf('.') + 1)..].Trim().Trim('"');
                        var property = properties[column];
                        var converter = property.GetTypeMapping().Converter;
                        rows.Columns.Add(column, converter?.ProviderClrType ?? Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType);
                        var value = column == "xmin" ? 1U : property.PropertyInfo?.GetValue(staff);
                        values.Add(converter is null ? value : converter.ConvertToProvider(value));
                    }

                    rows.Rows.Add(values.ToArray());
                }

                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(rows.CreateDataReader()));
            }
        }

        private sealed class SuppressConnectionOpen : DbConnectionInterceptor
        {
            public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
                DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult.Suppress());
        }

        private sealed class UnexpectedAuditWriter : ITenantAuditWriter
        {
            public Task<Result> WriteAsync(TenantAuditEntry entry, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("No staff reactivation transition should have been audited.");
        }
    }
}
