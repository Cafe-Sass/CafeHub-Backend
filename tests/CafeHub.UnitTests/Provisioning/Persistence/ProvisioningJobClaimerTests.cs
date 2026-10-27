using System.Data;
using System.Data.Common;
using CafeHub.Application.Provisioning;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CafeHub.UnitTests.Provisioning.Persistence
{
    public sealed class ProvisioningJobClaimerTests
    {
        [Fact]
        public async Task TryClaimNextAsync_UsesNamedParametersAndPreservesAtomicClaimQuery()
        {
            var jobId = Guid.CreateVersion7();
            var tenantId = Guid.CreateVersion7();
            var interceptor = new ClaimCommandInterceptor(jobId, tenantId);
            var options = new DbContextOptionsBuilder<MasterDbContext>()
                .UseNpgsql("Host=192.0.2.1;Database=unit-test;Timeout=1")
                .AddInterceptors(interceptor, new SuppressConnectionOpenInterceptor())
                .Options;
            await using var context = new MasterDbContext(options);
            var claimer = new ProvisioningJobClaimer(context);
            var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

            var result = await claimer.TryClaimNextAsync("worker-1", now, TimeSpan.FromMinutes(5), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(jobId, result.Value!.JobId);
            Assert.Equal(tenantId, result.Value.TenantId);

            var command = Assert.IsType<CapturedDbCommand>(interceptor.Command);
            Assert.Contains("lease_expires_at = @leaseExpiry", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("leased_by = @workerId", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("updated_at = @now", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("due_at IS NULL OR due_at <= @now", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("ORDER BY created_at", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("FOR UPDATE SKIP LOCKED", command.CommandText, StringComparison.Ordinal);
            Assert.Equal(
                new[] { "leaseExpiry", "workerId", "now" },
                command.Parameters.Select(parameter => parameter.ParameterName));
            Assert.Equal(now.AddMinutes(5), command.Parameters[0].Value);
            Assert.Equal("worker-1", command.Parameters[1].Value);
            Assert.Equal(now, command.Parameters[2].Value);
        }

        private sealed class ClaimCommandInterceptor(Guid jobId, Guid tenantId) : DbCommandInterceptor
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
                rows.Columns.Add("Id", typeof(Guid));
                rows.Columns.Add("TenantId", typeof(Guid));
                rows.Rows.Add(jobId, tenantId);

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
