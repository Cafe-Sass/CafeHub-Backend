using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using Microsoft.Extensions.Logging;

namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Applies the Tenant migration to a named tenant database and seeds the Owner account, the
    /// cafe profile shell, and the reference data idempotently. The owner password hash is copied
    /// from the approved onboarding request and never re-hashed, logged, or returned.
    /// </summary>
    internal sealed class TenantSchemaInitializer : ITenantSchemaInitializer
    {
        private readonly ITenantMigrationApplicator _migrationApplicator;
        private readonly ITenantSeeder _seeder;
        private readonly ILogger<TenantSchemaInitializer> _logger;

        public TenantSchemaInitializer(
            ITenantMigrationApplicator migrationApplicator,
            ITenantSeeder seeder,
            ILogger<TenantSchemaInitializer> logger)
        {
            _migrationApplicator = migrationApplicator ?? throw new ArgumentNullException(nameof(migrationApplicator));
            _seeder = seeder ?? throw new ArgumentNullException(nameof(seeder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result> InitializeAsync(
            TenantInitializationRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.TenantId == Guid.Empty)
            {
                return Result.Failure(ProvisioningErrors.InvalidTenantId());
            }

            if (!TenantDatabaseNaming.IsValidTenantSlug(request.TenantSlug))
            {
                return Result.Failure(ProvisioningErrors.InvalidTenantSlug());
            }

            var databaseName = TenantDatabaseNaming.DatabaseName(request.TenantSlug);

            try
            {
                await _migrationApplicator.ApplyAsync(databaseName, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Tenant migration failed for {DatabaseName} ({ExceptionType}).",
                    databaseName,
                    exception.GetType().Name);
                return Result.Failure(ProvisioningErrors.MigrationFailed());
            }

            var seedData = BuildSeedData(request, databaseName);

            try
            {
                await _seeder.SeedAsync(seedData, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Tenant seeding failed for {DatabaseName} ({ExceptionType}).",
                    databaseName,
                    exception.GetType().Name);
                return Result.Failure(ProvisioningErrors.SeedingFailed());
            }

            return Result.Success();
        }

        private static TenantSeedData BuildSeedData(TenantInitializationRequest request, string databaseName)
        {
            return new TenantSeedData(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                request.OwnerEmail.ToLowerInvariant(),
                request.OwnerFullName,
                request.OwnerPhoneNumber,
                request.OwnerPasswordHash,
                request.CafeName,
                "UTC",
                Guid.CreateVersion7().ToString("N"),
                Guid.CreateVersion7().ToString("N"),
                request.TenantSlug,
                request.TenantSlug,
                databaseName);
        }
    }
}
