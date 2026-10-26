using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Infrastructure.Provisioning;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests.Provisioning.Infrastructure
{
    public sealed class TenantSchemaInitializerTests
    {
        private static readonly Guid TenantId = Guid.CreateVersion7();
        private const string TenantSlug = "cool-cafe";
        private const string OwnerEmail = "Owner@Example.COM";
        private const string OwnerFullName = "Cafe Owner";
        private const string OwnerPasswordHash = "AQAAAAIAAYagAAAA...";
        private const string CafeName = "Cool Cafe";

        private static readonly TenantInitializationRequest Request = new(
            TenantId,
            TenantSlug,
            OwnerEmail,
            OwnerFullName,
            null,
            OwnerPasswordHash,
            CafeName,
            "plan-monthly");

        private const string DatabaseName = "cafehub_tenant_cool_cafe";

        private readonly FakeMigrationApplicator _migrationApplicator = new();
        private readonly FakeSeeder _seeder = new();
        private readonly CapturingLogger _logger = new();

        private TenantSchemaInitializer CreateInitializer() =>
            new(_migrationApplicator, _seeder, _logger);

        [Fact]
        public async Task InitializeAsync_AppliesMigrationToTenantDatabaseOnly_NeverMaster()
        {
            var result = await CreateInitializer().InitializeAsync(Request, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Single(_migrationApplicator.AppliedDatabases);
            Assert.Equal(DatabaseName, _migrationApplicator.AppliedDatabases[0]);
            Assert.DoesNotContain(_migrationApplicator.AppliedDatabases, d =>
                d.Equals("master", StringComparison.OrdinalIgnoreCase) ||
                d.Equals("postgres", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task InitializeAsync_SeedsWithCopiedHashAndLowercasedEmail()
        {
            var result = await CreateInitializer().InitializeAsync(Request, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Single(_seeder.Seeded);
            var seed = _seeder.Seeded[0];
            Assert.Equal(OwnerEmail.ToLowerInvariant(), seed.OwnerEmail);
            Assert.Equal(OwnerPasswordHash, seed.OwnerPasswordHash);
            Assert.Equal(DatabaseName, seed.DatabaseName);
        }

        [Fact]
        public async Task InitializeAsync_DoesNotSeed_WhenMigrationFails()
        {
            _migrationApplicator.FailureToThrow = new InvalidOperationException(
                "connection failed: Host=db;Password=leak-secret");

            var result = await CreateInitializer().InitializeAsync(Request, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(ProvisioningErrors.MigrationFailedCode, result.Error!.Code);
            Assert.Empty(_seeder.Seeded);
            AssertDoesNotContainSecrets();
        }

        [Fact]
        public async Task InitializeAsync_ReturnsSeedingFailed_WhenSeedingFails_WithoutSecrets()
        {
            _migrationApplicator.FailureToThrow = null;
            _seeder.FailureToThrow = new InvalidOperationException(
                "connection failed: Host=db;Password=leak-secret");

            var result = await CreateInitializer().InitializeAsync(Request, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(ProvisioningErrors.SeedingFailedCode, result.Error!.Code);
            Assert.Single(_migrationApplicator.AppliedDatabases);
            AssertDoesNotContainSecrets();
        }

        [Fact]
        public async Task InitializeAsync_LogsNoSecrets_OnFailure()
        {
            _seeder.FailureToThrow = new InvalidOperationException(
                "failed: Hash=AQAAAAIAAYagAAAA...; Host=db;Password=leak-secret");

            var result = await CreateInitializer().InitializeAsync(Request, CancellationToken.None);

            Assert.True(result.IsFailure);
            AssertDoesNotContainSecrets();
            Assert.DoesNotContain(_logger.Messages, m => m.Contains(OwnerPasswordHash, StringComparison.Ordinal));
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("leak-secret", StringComparison.Ordinal));
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("Host=", StringComparison.Ordinal));
        }

        [Fact]
        public async Task InitializeAsync_WithEmptyTenantId_ReturnsInvalidTenantId_WithoutCallingInfrastructure()
        {
            var result = await CreateInitializer().InitializeAsync(
                Request with { TenantId = Guid.Empty }, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(ProvisioningErrors.InvalidTenantIdCode, result.Error!.Code);
            Assert.Empty(_migrationApplicator.AppliedDatabases);
            Assert.Empty(_seeder.Seeded);
        }

        [Fact]
        public async Task InitializeAsync_WithInvalidSlug_ReturnsInvalidTenantSlug_WithoutCallingInfrastructure()
        {
            var result = await CreateInitializer().InitializeAsync(
                Request with { TenantSlug = "Bad_Slug" }, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(ProvisioningErrors.InvalidTenantSlugCode, result.Error!.Code);
            Assert.Empty(_migrationApplicator.AppliedDatabases);
            Assert.Empty(_seeder.Seeded);
        }

        [Fact]
        public void TenantInitializationRequest_HasNoRawPasswordMember()
        {
            var propertyNames = typeof(TenantInitializationRequest)
                .GetProperties()
                .Select(p => p.Name)
                .ToHashSet(StringComparer.Ordinal);

            Assert.DoesNotContain("Password", propertyNames, StringComparer.Ordinal);
            Assert.DoesNotContain("OwnerPassword", propertyNames, StringComparer.Ordinal);
            Assert.Contains("OwnerPasswordHash", propertyNames, StringComparer.Ordinal);
        }

        private void AssertDoesNotContainSecrets()
        {
            Assert.DoesNotContain(_logger.Messages, m => m.Contains(OwnerPasswordHash, StringComparison.Ordinal));
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("leak-secret", StringComparison.Ordinal));
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("Host=", StringComparison.Ordinal));
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("Password=", StringComparison.Ordinal));
        }

        private sealed class FakeMigrationApplicator : ITenantMigrationApplicator
        {
            public List<string> AppliedDatabases { get; } = [];
            public Exception? FailureToThrow { get; set; }

            public Task ApplyAsync(string databaseName, CancellationToken cancellationToken)
            {
                AppliedDatabases.Add(databaseName);
                if (FailureToThrow is not null)
                {
                    throw FailureToThrow;
                }

                return Task.CompletedTask;
            }
        }

        private sealed class FakeSeeder : ITenantSeeder
        {
            public List<TenantSeedData> Seeded { get; } = [];
            public Exception? FailureToThrow { get; set; }

            public Task<Result> SeedAsync(TenantSeedData seedData, CancellationToken cancellationToken)
            {
                Seeded.Add(seedData);
                if (FailureToThrow is not null)
                {
                    throw FailureToThrow;
                }

                return Task.FromResult(Result.Success());
            }
        }

        private sealed class CapturingLogger : ILogger<TenantSchemaInitializer>
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Messages.Add(formatter(state, exception)!);
            }
        }
    }
}
