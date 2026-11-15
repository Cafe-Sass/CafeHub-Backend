using System.Data;
using System.Data.Common;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Authentication;
using CafeHub.Infrastructure.Identity;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CafeHub.UnitTests
{
    public sealed class TenantRefreshTokenStoreTests
    {
        [Fact]
        public async Task TokenReadDiscardsTokenStateFromAPreviousTransaction()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=192.0.2.1;Database=unit-test;Timeout=1")
                .AddInterceptors(new SuppressConnectionOpen(), new EmptyTokenRead())
                .Options;
            await using var context = new TenantDbContext(options);
            var now = DateTimeOffset.UtcNow;
            var account = TenantUser.Create(
                Guid.NewGuid(), TenantUserKind.Owner, "owner", "owner", "Owner", "owner@example.test",
                "owner@example.test", null, "test-hash", "test-stamp", "test-concurrency-stamp", now);
            var trackedToken = TenantRefreshToken.Issue(
                Guid.NewGuid(), account.Id, Guid.NewGuid(), new string('a', 64), now, now.AddDays(7));
            context.Attach(account);
            context.Attach(trackedToken);
            var store = new TenantRefreshTokenStore(context, new TenantAccountReader(context));

            Assert.Null(await store.FindByTokenHashAsync(trackedToken.TokenHash, default));

            Assert.Equal(EntityState.Detached, context.Entry(trackedToken).State);
            Assert.Equal(EntityState.Unchanged, context.Entry(account).State);
        }

        private sealed class EmptyTokenRead : DbCommandInterceptor
        {
            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(new DataTable().CreateDataReader()));
        }

        private sealed class SuppressConnectionOpen : DbConnectionInterceptor
        {
            public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
                DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult.Suppress());
        }
    }
}
