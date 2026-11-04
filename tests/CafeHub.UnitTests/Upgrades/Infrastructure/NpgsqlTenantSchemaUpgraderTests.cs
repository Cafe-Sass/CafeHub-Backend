using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Application.Upgrades;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Upgrades;
using Npgsql;

namespace CafeHub.UnitTests.Upgrades.Infrastructure
{
    public sealed class NpgsqlTenantSchemaUpgraderTests
    {
        private sealed class FakeMigrationApplicator : ITenantMigrationApplicator
        {
            public Exception? ApplyFailure { get; set; }
            public Exception? ReadFailure { get; set; }
            public string? LatestAppliedMigrationId { get; set; } = "applied";
            public List<string> AppliedDatabases { get; } = [];

            public Task ApplyAsync(string databaseName, CancellationToken cancellationToken)
            {
                AppliedDatabases.Add(databaseName);

                if (ApplyFailure is not null)
                {
                    throw ApplyFailure;
                }

                return Task.CompletedTask;
            }

            public Task<string?> GetLatestAppliedMigrationIdAsync(
                string databaseName,
                CancellationToken cancellationToken)
            {
                if (ReadFailure is not null)
                {
                    throw ReadFailure;
                }

                return Task.FromResult(LatestAppliedMigrationId);
            }

            public Task<string?> GetLatestMigrationIdAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult<string?>("latest");
            }
        }

        private static NpgsqlTenantSchemaUpgrader CreateUpgrader(FakeMigrationApplicator applicator)
        {
            return new NpgsqlTenantSchemaUpgrader(applicator);
        }

        [Fact]
        public async Task UpgradeAsync_AppliesTheMigrationAndReturnsTheLatestAppliedId()
        {
            var applicator = new FakeMigrationApplicator();

            var result = await CreateUpgrader(applicator).UpgradeAsync(
                "cafehub_tenant_my_cafe",
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("applied", result.Value);
            Assert.Equal("cafehub_tenant_my_cafe", Assert.Single(applicator.AppliedDatabases));
        }

        [Fact]
        public async Task UpgradeAsync_MapsPostgresExceptionsToMigrationFailed()
        {
            var applicator = new FakeMigrationApplicator
            {
                ApplyFailure = new PostgresException("boom", "ERROR", "ERROR", "42P01"),
            };

            var result = await CreateUpgrader(applicator).UpgradeAsync(
                "cafehub_tenant_my_cafe",
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeFailureCodes.MigrationFailed, result.Error!.Code);
        }

        [Fact]
        public async Task UpgradeAsync_MapsProviderExceptionsToConnectionFailed()
        {
            var applicator = new FakeMigrationApplicator
            {
                ApplyFailure = new NpgsqlException("boom"),
            };

            var result = await CreateUpgrader(applicator).UpgradeAsync(
                "cafehub_tenant_my_cafe",
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeFailureCodes.ConnectionFailed, result.Error!.Code);
        }

        [Fact]
        public async Task UpgradeAsync_MapsTimeoutsToConnectionFailed()
        {
            var applicator = new FakeMigrationApplicator
            {
                ApplyFailure = new TimeoutException("boom"),
            };

            var result = await CreateUpgrader(applicator).UpgradeAsync(
                "cafehub_tenant_my_cafe",
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeFailureCodes.ConnectionFailed, result.Error!.Code);
        }

        [Fact]
        public async Task UpgradeAsync_MapsUnexpectedExceptionsToUnexpected()
        {
            var applicator = new FakeMigrationApplicator
            {
                ApplyFailure = new InvalidOperationException("boom"),
            };

            var result = await CreateUpgrader(applicator).UpgradeAsync(
                "cafehub_tenant_my_cafe",
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeFailureCodes.Unexpected, result.Error!.Code);
        }

        [Fact]
        public async Task UpgradeAsync_MapsReadFailuresAfterAMigrationToTheClosedCode()
        {
            var applicator = new FakeMigrationApplicator
            {
                ReadFailure = new NpgsqlException("boom"),
            };

            var result = await CreateUpgrader(applicator).UpgradeAsync(
                "cafehub_tenant_my_cafe",
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeFailureCodes.ConnectionFailed, result.Error!.Code);
        }

        [Fact]
        public async Task UpgradeAsync_PropagatesCancellation()
        {
            var applicator = new FakeMigrationApplicator
            {
                ApplyFailure = new OperationCanceledException(),
            };

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                CreateUpgrader(applicator).UpgradeAsync("cafehub_tenant_my_cafe", CancellationToken.None));
        }
    }
}
