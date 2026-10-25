using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Domain.Identity;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CafeHub.Infrastructure.Identity
{
    internal sealed class SuperAdminSeeder : ISuperAdminSeeder
    {
        private const string CreatedAuditAction = "admin.seed.created.succeeded";
        private const string PlatformAdminSubjectType = "platform_admin";

        private readonly UserManager<PlatformAdminUser> userManager;
        private readonly RoleManager<PlatformAdminRole> roleManager;
        private readonly IReadOnlyCollection<IPasswordValidator<PlatformAdminUser>> passwordValidators;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly IMasterUnitOfWork masterUnitOfWork;
        private readonly TimeProvider timeProvider;
        private readonly PlatformSeedOptions options;
        private readonly ILogger<SuperAdminSeeder> logger;

        public SuperAdminSeeder(
            UserManager<PlatformAdminUser> userManager,
            RoleManager<PlatformAdminRole> roleManager,
            IEnumerable<IPasswordValidator<PlatformAdminUser>> passwordValidators,
            IPlatformAuditWriter auditWriter,
            IMasterUnitOfWork masterUnitOfWork,
            TimeProvider timeProvider,
            PlatformSeedOptions options,
            ILogger<SuperAdminSeeder> logger)
        {
            this.userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            this.roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            this.passwordValidators = passwordValidators?.ToArray() ?? throw new ArgumentNullException(nameof(passwordValidators));
            this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
            this.masterUnitOfWork = masterUnitOfWork ?? throw new ArgumentNullException(nameof(masterUnitOfWork));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<SeedOutcome>> SeedAsync(CancellationToken cancellationToken)
        {
            var configuredEmail = options.SuperAdminEmail;
            if (string.IsNullOrWhiteSpace(configuredEmail))
            {
                logger.LogInformation(
                    "Super admin seeding skipped because {ConfigurationPath} is not set.",
                    PlatformSeedConfigurationKeys.SuperAdminEmailPath);

                return Result.Success(SeedOutcome.SkippedNotConfigured);
            }

            var password = options.SuperAdminPassword;
            if (string.IsNullOrWhiteSpace(password))
            {
                return InvalidConfiguration();
            }

            var email = PlatformAdminEmail.Normalize(configuredEmail);
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null)
            {
                logger.LogInformation("Super admin seed outcome {Outcome}.", SeedOutcome.AlreadyExists);

                return Result.Success(SeedOutcome.AlreadyExists);
            }

            var now = timeProvider.GetUtcNow();
            var administrator = new PlatformAdminUser(email)
            {
                Id = Guid.CreateVersion7(),
                Email = email,
                CreatedAt = now,
                UpdatedAt = now,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            };

            if (!await IsPasswordAllowedAsync(administrator, password))
            {
                return InvalidConfiguration();
            }

            await masterUnitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var role = await roleManager.FindByNameAsync(PlatformAdminRoles.SuperAdmin);
                if (role is null)
                {
                    role = new PlatformAdminRole(PlatformAdminRoles.SuperAdmin)
                    {
                        Id = Guid.CreateVersion7(),
                        CreatedAt = now,
                        UpdatedAt = now,
                    };

                    var roleCreation = await roleManager.CreateAsync(role);
                    if (!roleCreation.Succeeded)
                    {
                        return await RollbackWithInvalidConfigurationAsync(cancellationToken);
                    }
                }

                var administratorCreation = await userManager.CreateAsync(administrator, password);
                if (!administratorCreation.Succeeded)
                {
                    return await RollbackWithInvalidConfigurationAsync(cancellationToken);
                }

                var membership = await userManager.AddToRoleAsync(administrator, PlatformAdminRoles.SuperAdmin);
                if (!membership.Succeeded)
                {
                    return await RollbackWithInvalidConfigurationAsync(cancellationToken);
                }

                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.System,
                    Action = CreatedAuditAction,
                    TargetType = PlatformAdminSubjectType,
                    TargetId = administrator.Id,
                    Outcome = PlatformAuditOutcome.Succeeded,
                });

                await masterUnitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await masterUnitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }

            logger.LogInformation(
                "Super admin seed outcome {Outcome} for administrator {AdministratorId}.",
                SeedOutcome.Created,
                administrator.Id);

            return Result.Success(SeedOutcome.Created);
        }

        private async Task<bool> IsPasswordAllowedAsync(PlatformAdminUser administrator, string password)
        {
            foreach (var validator in passwordValidators)
            {
                var validation = await validator.ValidateAsync(userManager, administrator, password);
                if (!validation.Succeeded)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<Result<SeedOutcome>> RollbackWithInvalidConfigurationAsync(CancellationToken cancellationToken)
        {
            await masterUnitOfWork.RollbackTransactionAsync(cancellationToken);

            return InvalidConfiguration();
        }

        private Result<SeedOutcome> InvalidConfiguration()
        {
            logger.LogWarning("Super admin seeding stopped because the platform seed configuration is invalid.");

            return Result.Failure<SeedOutcome>(PlatformSeedErrors.InvalidConfiguration());
        }
    }
}
