using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Menu;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.Infrastructure.Provisioning;
using CafeHub.Infrastructure.Upgrades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CafeHub.UnitTests.Menu.Persistence
{
    public sealed class Task47MigrationPostgreSqlTests
    {
        private const string Legacy = "20261116122028_AddMenuProducts";
        private const string Expansion = "20261117233117_ExpandTask47ProductRemediation";
        private const string Finalization = "20261117233501_FinalizeTask47ProductTagIdentity";

        [Task47PostgreSqlFact]
        public async Task RoutineRunnerStopsAtExpansionAndMaintainsReadinessPermissions()
        {
            await using var fresh = await Task47PostgreSqlDatabase.CreateAsync();
            var applicator = new NpgsqlTenantMigrationApplicator(new() { AdminConnectionString = fresh.ConnectionString });

            Assert.Equal(Expansion, await applicator.GetLatestMigrationIdAsync(default));
            await applicator.ApplyAsync(fresh.Name, default);
            Assert.Equal(Expansion, await applicator.GetLatestAppliedMigrationIdAsync(fresh.Name, default));
            await AssertReadinessPermissionsAsync(fresh);

            await using var upgraded = await Task47PostgreSqlDatabase.CreateAsync();
            await using var context = upgraded.CreateContext();
            await context.GetService<IMigrator>().MigrateAsync(Legacy);
            await using (var admin = upgraded.CreateConnection())
            {
                await admin.OpenAsync();
                var role = Task47PostgreSqlDatabase.Quote(upgraded.ApplicationRole);
                await Task47PostgreSqlDatabase.ExecuteAsync(admin,
                    $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {role}");
            }

            var runner = new NpgsqlTenantSchemaUpgrader(applicator);
            var result = await runner.UpgradeAsync(upgraded.Name, default);
            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(Expansion, result.Value);
            await AssertReadinessPermissionsAsync(upgraded);

            await context.GetService<IMigrator>().MigrateAsync(Finalization);
            result = await runner.UpgradeAsync(upgraded.Name, default);
            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(Finalization, result.Value);
            await AssertReadinessPermissionsAsync(upgraded);
        }

        [Task47PostgreSqlFact]
        public Task ExpansionRollbackWaitsForAnInFlightWriteAndPreservesIt() =>
            AssertRollbackWaitsForWriterAsync(Expansion, Legacy, "remediation data must be preserved", false);

        [Task47PostgreSqlFact]
        public Task FinalizationRollbackWaitsForAnInFlightWriteAndPreservesIt() =>
            AssertRollbackWaitsForWriterAsync(Finalization, Expansion, "cutover state prevents a lossless downgrade", true);

        [Task47PostgreSqlFact]
        public async Task FinalizationWaitsForWriterAndRejectsItsCommittedInvalidLabel()
        {
            await using var database = await Task47PostgreSqlDatabase.CreateAsync();
            await using var context = database.CreateContext();
            await SeedExpandedTagAsync(context);
            await context.Database.OpenConnectionAsync();
            var finalizerPid = ((NpgsqlConnection)context.Database.GetDbConnection()).ProcessID;

            await using var writer = database.CreateConnection();
            await writer.OpenAsync();
            await using var transaction = await writer.BeginTransactionAsync();
            await Task47PostgreSqlDatabase.ExecuteAsync(writer, "UPDATE product_tags SET tag = ' coffee '");

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var finalization = context.GetService<IMigrator>().MigrateAsync(Finalization, timeout.Token);
            await WaitForBlockAsync(writer, writer.ProcessID, finalizerPid, finalization, timeout.Token);
            await transaction.CommitAsync(timeout.Token);

            var error = await Assert.ThrowsAsync<PostgresException>(() => finalization);
            Assert.Contains("backfill and normalization must be verified", error.MessageText);
            Assert.Equal(Expansion, (await context.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal(" coffee ", (await context.ProductTags.AsNoTracking().SingleAsync()).Tag);

            await Task47PostgreSqlDatabase.ExecuteAsync(writer, "UPDATE product_tags SET tag = 'coffee'");
            await context.GetService<IMigrator>().MigrateAsync(Finalization);
            Assert.Equal(Finalization, (await context.Database.GetAppliedMigrationsAsync()).Last());
        }

        [Task47PostgreSqlFact]
        public async Task WriterWaitsForFinalizationAndTheFinalSchemaRejectsItsInvalidLabel()
        {
            await using var database = await Task47PostgreSqlDatabase.CreateAsync();
            await using var context = database.CreateContext();
            await SeedExpandedTagAsync(context);
            await context.Database.OpenConnectionAsync();
            var finalizer = (NpgsqlConnection)context.Database.GetDbConnection();

            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.GetService<IMigrator>().MigrateAsync(Finalization);
            await using var writer = database.CreateConnection();
            await writer.OpenAsync();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var write = Task47PostgreSqlDatabase.ExecuteAsync(writer, "UPDATE product_tags SET tag = ' coffee '");
            await WaitForBlockAsync(finalizer, finalizer.ProcessID, writer.ProcessID, write, timeout.Token);
            await transaction.CommitAsync(timeout.Token);

            var error = await Assert.ThrowsAsync<PostgresException>(() => write);
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal("ck_product_tags_normalized_label", error.ConstraintName);
            Assert.Equal(Finalization, (await context.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal("coffee", (await context.ProductTags.AsNoTracking().SingleAsync()).Tag);
            await Task47PostgreSqlDatabase.ExecuteAsync(writer, "UPDATE product_tags SET tag = 'espresso'");
            Assert.Equal("espresso", (await context.ProductTags.AsNoTracking().SingleAsync()).Tag);
        }

        [Task47PostgreSqlFact]
        public async Task CleanRollbackWaitsForCatalogUseAndGateRechecksTheNewSchema()
        {
            await using var database = await Task47PostgreSqlDatabase.CreateAsync();
            await using var context = database.CreateContext();
            await context.GetService<IMigrator>().MigrateAsync(Finalization);
            await context.Database.OpenConnectionAsync();
            var catalogConnection = (NpgsqlConnection)context.Database.GetDbConnection();
            var catalog = new ProductCatalogSchema(context);

            await using var transaction = await context.Database.BeginTransactionAsync();
            Assert.True((await catalog.RunAsync(() => Task.FromResult(Result.Success(true)), default)).IsSuccess);

            await using var rollbackContext = database.CreateContext();
            await rollbackContext.Database.OpenConnectionAsync();
            var rollbackPid = ((NpgsqlConnection)rollbackContext.Database.GetDbConnection()).ProcessID;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var rollback = rollbackContext.GetService<IMigrator>().MigrateAsync(Legacy, timeout.Token);
            await WaitForBlockAsync(catalogConnection, catalogConnection.ProcessID, rollbackPid, rollback, timeout.Token);
            await transaction.CommitAsync(timeout.Token);
            await rollback;

            Assert.Equal(Legacy, (await context.Database.GetAppliedMigrationsAsync()).Last());
            var gated = await catalog.RunAsync<bool>(() => throw new InvalidOperationException("Catalog operation leaked."), default);
            Assert.Equal(ProductErrors.CatalogSchemaNotReady(), gated.Error);
        }

        private static async Task SeedExpandedTagAsync(TenantDbContext context)
        {
            await context.GetService<IMigrator>().MigrateAsync(Expansion);
            var now = DateTimeOffset.UtcNow;
            var actor = TenantUser.Create(Guid.CreateVersion7(), TenantUserKind.Owner, "fixture-owner", "FIXTURE-OWNER",
                "Fixture Owner", "owner@example.test", "OWNER@EXAMPLE.TEST", null, "fixture-hash",
                "fixture-stamp", "fixture-version", now);
            var category = MenuCategory.Create(Guid.CreateVersion7(), null, "Coffee", 0, now, actor.Id);
            var size = ProductSize.Create(Guid.CreateVersion7(), "Regular", 5m, true, now, actor.Id);
            var product = Product.Create(Guid.CreateVersion7(), category.Id, null, "Coffee", null, null,
                ProductKind.Drink, false, null, 0, [size], [], ["coffee"], now, actor.Id);
            context.AddRange(actor, category, product);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        private static async Task WaitForBlockAsync(
            NpgsqlConnection observer, int blockerPid, int blockedPid, Task pending, CancellationToken cancellationToken)
        {
            await using var check = new NpgsqlCommand("SELECT @blocker = ANY (pg_blocking_pids(@blocked))", observer);
            check.Parameters.AddWithValue("blocker", blockerPid);
            check.Parameters.AddWithValue("blocked", blockedPid);
            while (!pending.IsCompleted)
            {
                if ((bool)(await check.ExecuteScalarAsync(cancellationToken))!)
                {
                    return;
                }

                await Task.Delay(20, cancellationToken);
            }

            Assert.Fail("Expected the second PostgreSQL session to wait for the first session's table lock.");
        }

        private static async Task AssertRollbackWaitsForWriterAsync(
            string current, string target, string expectedGuard, bool lockTags)
        {
            await using var database = await Task47PostgreSqlDatabase.CreateAsync();
            await using var context = database.CreateContext();
            await context.GetService<IMigrator>().MigrateAsync(current);
            await context.Database.OpenConnectionAsync();
            var rollbackPid = ((NpgsqlConnection)context.Database.GetDbConnection()).ProcessID;

            await using var writer = database.CreateConnection();
            await writer.OpenAsync();
            await using var transaction = await writer.BeginTransactionAsync();
            if (lockTags)
            {
                await Task47PostgreSqlDatabase.ExecuteAsync(writer, "LOCK TABLE product_tags IN ROW EXCLUSIVE MODE");
            }

            await Task47PostgreSqlDatabase.ExecuteAsync(writer,
                "INSERT INTO product_routing_readiness (singleton, verified_at) VALUES (TRUE, CURRENT_TIMESTAMP)");

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var rollback = context.GetService<IMigrator>().MigrateAsync(target, timeout.Token);
            var blocked = false;
            try
            {
                await using var check = new NpgsqlCommand("SELECT @writer = ANY (pg_blocking_pids(@rollback))", writer);
                check.Parameters.AddWithValue("writer", writer.ProcessID);
                check.Parameters.AddWithValue("rollback", rollbackPid);
                while (!rollback.IsCompleted && !blocked)
                {
                    blocked = (bool)(await check.ExecuteScalarAsync(timeout.Token))!;
                    if (!blocked)
                    {
                        await Task.Delay(20, timeout.Token);
                    }
                }
            }
            finally
            {
                await transaction.CommitAsync();
            }

            var exception = await Assert.ThrowsAsync<PostgresException>(() => rollback);
            Assert.True(blocked);
            Assert.Contains(expectedGuard, exception.MessageText);
            Assert.Equal(current, (await context.Database.GetAppliedMigrationsAsync()).Last());
            Assert.True(await context.ProductRoutingReadiness.AnyAsync());
        }

        private static async Task AssertReadinessPermissionsAsync(Task47PostgreSqlDatabase database)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            await Task47PostgreSqlDatabase.ExecuteAsync(connection, $"SET ROLE {Task47PostgreSqlDatabase.Quote(database.ApplicationRole)}");
            await using var check = new NpgsqlCommand("SELECT COUNT(*) FROM product_routing_readiness", connection);
            Assert.Equal(0L, (long)(await check.ExecuteScalarAsync())!);

            foreach (var sql in new[]
            {
                "INSERT INTO product_routing_readiness VALUES (TRUE, CURRENT_TIMESTAMP)",
                "UPDATE product_routing_readiness SET verified_at = CURRENT_TIMESTAMP",
                "DELETE FROM product_routing_readiness",
                "TRUNCATE product_routing_readiness",
            })
            {
                var exception = await Assert.ThrowsAsync<PostgresException>(
                    () => Task47PostgreSqlDatabase.ExecuteAsync(connection, sql));
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
            }
        }
    }
}
