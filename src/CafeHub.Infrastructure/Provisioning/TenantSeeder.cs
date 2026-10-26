using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Seeds the Owner account, the Owner role and membership, the cafe profile shell, and the
    /// provisioning audit record into a tenant database in one idempotent transaction. Existing
    /// rows are never overwritten or duplicated.
    /// </summary>
    internal sealed class TenantSeeder : ITenantSeeder
    {
        private const string OwnerRoleName = "Owner";
        private const string ProvisionedActionCode = "tenant.provisioned.seeded";

        private readonly TenantSchemaInitializerOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<TenantSeeder> _logger;

        public TenantSeeder(
            TenantSchemaInitializerOptions options,
            TimeProvider timeProvider,
            ILogger<TenantSeeder> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result> SeedAsync(
            TenantSeedData seedData,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(seedData);

            var connectionString = new NpgsqlConnectionStringBuilder(_options.AdminConnectionString)
            {
                Database = seedData.DatabaseName,
            }.ConnectionString;

            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            using var context = new TenantDbContext(options);
            using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var now = _timeProvider.GetUtcNow();
                var normalizedEmail = (seedData.OwnerEmail ?? string.Empty).ToLowerInvariant();

                var owner = await context.TenantUsers
                    .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
                if (owner is null)
                {
                    owner = TenantUser.Create(
                        seedData.OwnerId,
                        TenantUserKind.Owner,
                        seedData.UserName,
                        seedData.NormalizedUserName,
                        seedData.OwnerFullName,
                        seedData.OwnerEmail,
                        normalizedEmail,
                        seedData.OwnerPhoneNumber,
                        seedData.OwnerPasswordHash,
                        seedData.SecurityStamp,
                        seedData.ConcurrencyStamp,
                        now);
                    context.TenantUsers.Add(owner);
                }

                var role = await context.TenantRoles
                    .FirstOrDefaultAsync(r => r.NormalizedName == OwnerRoleName.ToLowerInvariant(), cancellationToken);
                if (role is null)
                {
                    role = TenantRole.Create(
                        seedData.RoleId,
                        OwnerRoleName,
                        OwnerRoleName.ToLowerInvariant(),
                        Guid.CreateVersion7().ToString("N"),
                        now);
                    context.TenantRoles.Add(role);
                }

                if (!await context.TenantUserRoles
                    .AnyAsync(ur => ur.TenantUserId == owner.Id && ur.RoleId == role.Id, cancellationToken))
                {
                    context.TenantUserRoles.Add(
                        TenantUserRole.Create(seedData.UserRoleId, owner.Id, role.Id, now));
                }

                if (!await context.CafeSettings.AnyAsync(cancellationToken))
                {
                    context.CafeSettings.Add(CafeSettings.Create(
                        seedData.CafeSettingsId,
                        seedData.CafeName,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        seedData.Timezone,
                        now));
                }

                if (!await context.TenantAuditRecords
                    .AnyAsync(a => a.ActionCode == ProvisionedActionCode, cancellationToken))
                {
                    context.TenantAuditRecords.Add(TenantAuditRecord.Create(
                        seedData.AuditRecordId,
                        now,
                        null,
                        TenantAuditRecordKind.System,
                        ProvisionedActionCode,
                        "tenant",
                        null,
                        null,
                        null));
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success();
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
