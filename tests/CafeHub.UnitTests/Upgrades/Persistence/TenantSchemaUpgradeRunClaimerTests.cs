using System.Data;
using System.Data.Common;
using CafeHub.Application.Upgrades;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Upgrades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CafeHub.UnitTests.Upgrades.Persistence
{
    public sealed class TenantSchemaUpgradeRunClaimerTests
    {
        [Fact]
        public async Task TryClaimNextAsync_UsesNamedParametersAndPreservesAtomicClaimQuery()
        {
            var runId = Guid.CreateVersion7();
            var tenantId = Guid.CreateVersion7();
            var interceptor = new ClaimCommandInterceptor(runId, tenantId);
            await using var context = CreateContext(interceptor);
            var claimer = new TenantSchemaUpgradeRunClaimer(context);
            var now = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

            var result = await claimer.TryClaimNextAsync(
                "worker-1",
                now,
                TimeSpan.FromMinutes(5),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(runId, result.Value!.RunId);
            Assert.Equal(tenantId, result.Value.TenantId);

            var command = Assert.IsType<CapturedDbCommand>(interceptor.Command);
            Assert.Contains("UPDATE tenant_schema_upgrade_runs", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("status = 'Running'", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("attempt_count = attempt_count + 1", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("started_at = COALESCE(started_at, @now)", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("lease_expires_at = @leaseExpiry", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("leased_by = @workerId", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("updated_at = @now", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("status = 'Pending'", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("lease_expires_at <= @now", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("ORDER BY created_at", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("FOR UPDATE SKIP LOCKED", command.CommandText, StringComparison.Ordinal);
            Assert.Equal(
                new[] { "leaseExpiry", "workerId", "now" },
                command.Parameters.Select(parameter => parameter.ParameterName));
            Assert.Equal(now.AddMinutes(5), command.Parameters[0].Value);
            Assert.Equal("worker-1", command.Parameters[1].Value);
            Assert.Equal(now, command.Parameters[2].Value);
        }

        [Fact]
        public async Task TryClaimNextAsync_ReturnsNull_WhenNoRunIsClaimable()
        {
            var interceptor = new ClaimCommandInterceptor(null, null);
            await using var context = CreateContext(interceptor);
            var claimer = new TenantSchemaUpgradeRunClaimer(context);

            var result = await claimer.TryClaimNextAsync(
                "worker-1",
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(5),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task TryClaimNextAsync_RejectsABlankWorkerId(string workerId)
        {
            var interceptor = new ClaimCommandInterceptor(null, null);
            await using var context = CreateContext(interceptor);
            var claimer = new TenantSchemaUpgradeRunClaimer(context);

            var result = await claimer.TryClaimNextAsync(
                workerId,
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(5),
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.InvalidWorkerIdCode, result.Error!.Code);
            Assert.Null(interceptor.Command);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task TryClaimNextAsync_RejectsANonPositiveLeaseDuration(int leaseSeconds)
        {
            var interceptor = new ClaimCommandInterceptor(null, null);
            await using var context = CreateContext(interceptor);
            var claimer = new TenantSchemaUpgradeRunClaimer(context);

            var result = await claimer.TryClaimNextAsync(
                "worker-1",
                DateTimeOffset.UtcNow,
                TimeSpan.FromSeconds(leaseSeconds),
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.InvalidLeaseDurationCode, result.Error!.Code);
            Assert.Null(interceptor.Command);
        }

        private static MasterDbContext CreateContext(ClaimCommandInterceptor interceptor)
        {
            var options = new DbContextOptionsBuilder<MasterDbContext>()
                .UseNpgsql("Host=192.0.2.1;Database=unit-test;Timeout=1")
                .AddInterceptors(interceptor, new SuppressConnectionOpenInterceptor())
                .Options;

            return new MasterDbContext(options);
        }

        private sealed class ClaimCommandInterceptor(Guid? runId, Guid? tenantId) : DbCommandInterceptor
        {
            public CapturedDbCommand? Command { get; private set; }

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
            {
                Command = new CapturedDbCommand(command.CommandText, command.Parameters);

                var rows = new DataTable();
                rows.Columns.Add("RunId", typeof(Guid));
                rows.Columns.Add("TenantId", typeof(Guid));

                if (runId is not null && tenantId is not null)
                {
                    rows.Rows.Add(runId.Value, tenantId.Value);
                }

                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(rows.CreateDataReader()));
            }
        }

        private sealed class SuppressConnectionOpenInterceptor : DbConnectionInterceptor
        {
            public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
                DbConnection connection,
                ConnectionEventData eventData,
                InterceptionResult result,
                CancellationToken cancellationToken = default)
            {
                return ValueTask.FromResult(InterceptionResult.Suppress());
            }
        }

        private sealed class CapturedDbCommand(string commandText, DbParameterCollection parameters)
        {
            public string CommandText { get; } = commandText;
            public IReadOnlyList<CapturedDbParameter> Parameters { get; } = parameters
                .Cast<DbParameter>()
                .Select(parameter => new CapturedDbParameter(parameter.ParameterName, parameter.Value))
                .ToArray();
        }

        private sealed record CapturedDbParameter(string ParameterName, object? Value);
    }
}
