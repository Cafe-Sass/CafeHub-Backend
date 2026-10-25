using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Domain.Identity;
using CafeHub.Infrastructure.Identity;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CafeHub.UnitTests
{
    public sealed class SuperAdminSeederTests
    {
        private const string SeedEmail = "seed.superadmin@cafehub.app";
        private const string SeedPassword = "Str0ng!SeedPassword";
        private const string CreatedAuditAction = "admin.seed.created.succeeded";
        private const string PlatformAdminTargetType = "platform_admin";

        private static readonly DateTimeOffset FixedNow = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        private static SeedTestContext CreateContext(
            PlatformSeedOptions options,
            FakePlatformAdminUserStore? userStore = null,
            FakePlatformAdminRoleStore? roleStore = null)
        {
            userStore ??= new FakePlatformAdminUserStore();
            roleStore ??= new FakePlatformAdminRoleStore();
            var identityOptions = new IdentityOptions();
            PlatformAdminPasswordPolicy.Apply(identityOptions);
            var passwordValidators = new IPasswordValidator<PlatformAdminUser>[]
            {
                new PasswordValidator<PlatformAdminUser>(),
            };
            var userManager = new UserManager<PlatformAdminUser>(
                userStore,
                Options.Create(identityOptions),
                new PasswordHasher<PlatformAdminUser>(),
                new IUserValidator<PlatformAdminUser>[] { new UserValidator<PlatformAdminUser>() },
                passwordValidators,
                new PlatformAdminLookupNormalizer(),
                new IdentityErrorDescriber(),
                new ServiceCollection().BuildServiceProvider(),
                NullLogger<UserManager<PlatformAdminUser>>.Instance);
            var roleManager = new RoleManager<PlatformAdminRole>(
                roleStore,
                new IRoleValidator<PlatformAdminRole>[] { new RoleValidator<PlatformAdminRole>() },
                new PlatformAdminLookupNormalizer(),
                new IdentityErrorDescriber(),
                NullLogger<RoleManager<PlatformAdminRole>>.Instance);
            var auditWriter = new RecordingSuperAdminAuditWriter();
            var unitOfWork = new RecordingSeedUnitOfWork();
            var logger = new FakeLogger<SuperAdminSeeder>();
            var seeder = new SuperAdminSeeder(
                userManager,
                roleManager,
                passwordValidators,
                auditWriter,
                unitOfWork,
                new MutableTimeProvider { UtcNow = FixedNow },
                options,
                logger);

            return new SeedTestContext(
                seeder,
                userManager,
                userStore,
                roleStore,
                auditWriter,
                unitOfWork,
                logger);
        }

        private static PlatformAdminUser CreateExistingAdministrator(string email, string password)
        {
            var administrator = new PlatformAdminUser(email)
            {
                Id = Guid.CreateVersion7(),
                Email = email,
                NormalizedEmail = PlatformAdminEmail.Normalize(email),
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = FixedNow,
                UpdatedAt = FixedNow,
            };
            administrator.PasswordHash = new PasswordHasher<PlatformAdminUser>().HashPassword(administrator, password);

            return administrator;
        }

        private static string CapturedLogText(SeedTestContext context)
        {
            return string.Join(
                "\n",
                context.Logger.Entries.Select(entry => $"{entry.Message}\n{entry.State}"));
        }

        [Fact]
        public async Task TheFirstRun_CreatesTheRoleAndTheAdministrator_AndAuditsTheCreation_ThenTheSecondRunIsANoOp()
        {
            var context = CreateContext(new PlatformSeedOptions
            {
                SuperAdminEmail = "  Seed.SuperAdmin@CafeHub.App  ",
                SuperAdminPassword = SeedPassword,
            });

            var firstRun = await context.Seeder.SeedAsync(CancellationToken.None);

            Assert.True(firstRun.IsSuccess);
            Assert.Equal(SeedOutcome.Created, firstRun.Value);
            var administrator = Assert.Single(context.UserStore.Users);
            Assert.Equal(SeedEmail, administrator.Email);
            Assert.True(administrator.IsActive);
            Assert.NotNull(administrator.PasswordHash);
            var passwordHasher = new PasswordHasher<PlatformAdminUser>();
            Assert.Equal(
                PasswordVerificationResult.Success,
                passwordHasher.VerifyHashedPassword(administrator, administrator.PasswordHash, SeedPassword));
            Assert.Equal(
                PasswordVerificationResult.Failed,
                passwordHasher.VerifyHashedPassword(administrator, administrator.PasswordHash, "Different!Password34"));
            Assert.Equal(FixedNow, administrator.CreatedAt);
            Assert.Equal(FixedNow, administrator.UpdatedAt);
            var role = Assert.Single(context.RoleStore.Roles);
            Assert.Equal(PlatformAdminRoles.SuperAdmin, role.Name);
            Assert.Equal(FixedNow, role.CreatedAt);
            Assert.Equal(FixedNow, role.UpdatedAt);
            Assert.True(await context.UserManager.IsInRoleAsync(administrator, PlatformAdminRoles.SuperAdmin));
            var auditEntry = Assert.Single(context.AuditWriter.Entries);
            Assert.Equal(PlatformAuditActorKind.System, auditEntry.ActorKind);
            Assert.Equal(CreatedAuditAction, auditEntry.Action);
            Assert.Equal(PlatformAdminTargetType, auditEntry.TargetType);
            Assert.Equal(administrator.Id, auditEntry.TargetId);
            Assert.Equal(PlatformAuditOutcome.Succeeded, auditEntry.Outcome);
            Assert.Equal(1, context.UnitOfWork.BeginCount);
            Assert.Equal(1, context.UnitOfWork.CommitCount);
            Assert.Equal(0, context.UnitOfWork.RollbackCount);
            var originalHash = administrator.PasswordHash;

            var secondRun = await context.Seeder.SeedAsync(CancellationToken.None);

            Assert.True(secondRun.IsSuccess);
            Assert.Equal(SeedOutcome.AlreadyExists, secondRun.Value);
            Assert.Same(administrator, Assert.Single(context.UserStore.Users));
            Assert.Single(context.RoleStore.Roles);
            Assert.Equal(originalHash, administrator.PasswordHash);
            Assert.Single(context.AuditWriter.Entries);
            Assert.Equal(1, context.UnitOfWork.BeginCount);
            Assert.Equal(1, context.UnitOfWork.CommitCount);
        }

        [Fact]
        public async Task AnExistingAdministrator_KeepsTheStoredPasswordHash_WhenTheSecretPasswordDiffers()
        {
            const string originalPassword = "Original!Password12";
            const string differentPassword = "Different!Password34";
            var existing = CreateExistingAdministrator(SeedEmail, originalPassword);
            var userStore = new FakePlatformAdminUserStore(existing);
            var context = CreateContext(
                new PlatformSeedOptions
                {
                    SuperAdminEmail = " Seed.SuperAdmin@CafeHub.App ",
                    SuperAdminPassword = differentPassword,
                },
                userStore);

            var result = await context.Seeder.SeedAsync(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(SeedOutcome.AlreadyExists, result.Value);
            Assert.Same(existing, Assert.Single(userStore.Users));
            Assert.NotNull(existing.PasswordHash);
            var passwordHasher = new PasswordHasher<PlatformAdminUser>();
            Assert.Equal(
                PasswordVerificationResult.Success,
                passwordHasher.VerifyHashedPassword(existing, existing.PasswordHash, originalPassword));
            Assert.Equal(
                PasswordVerificationResult.Failed,
                passwordHasher.VerifyHashedPassword(existing, existing.PasswordHash, differentPassword));
            Assert.Empty(context.RoleStore.Roles);
            Assert.Empty(context.AuditWriter.Entries);
            Assert.Equal(0, context.UnitOfWork.BeginCount);
            Assert.Equal(0, context.UnitOfWork.CommitCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AnEmptySeedConfiguration_IsSkipped_WithoutCreatingOrAuditingAnything(string? email)
        {
            var context = CreateContext(new PlatformSeedOptions
            {
                SuperAdminEmail = email,
                SuperAdminPassword = SeedPassword,
            });

            var result = await context.Seeder.SeedAsync(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(SeedOutcome.SkippedNotConfigured, result.Value);
            Assert.Empty(context.UserStore.Users);
            Assert.Empty(context.RoleStore.Roles);
            Assert.Empty(context.AuditWriter.Entries);
            Assert.Equal(0, context.UnitOfWork.BeginCount);
            Assert.Equal(0, context.UnitOfWork.CommitCount);
            Assert.DoesNotContain(SeedPassword, CapturedLogText(context), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("Aa1!short")]
        [InlineData("weakpassword1!")]
        [InlineData("WeakPassword!!")]
        [InlineData("WeakPassword12")]
        [InlineData("WEAKPASSWORD1!")]
        public async Task AWeakSeedPassword_FailsWithTheSafeMessage_AndCreatesNothing(string password)
        {
            var context = CreateContext(new PlatformSeedOptions
            {
                SuperAdminEmail = SeedEmail,
                SuperAdminPassword = password,
            });

            var result = await context.Seeder.SeedAsync(CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(PlatformSeedErrors.InvalidConfigurationCode, result.Error!.Code);
            Assert.Equal(PlatformSeedErrors.InvalidConfigurationMessage, result.Error.Message);
            Assert.DoesNotContain(password, result.Error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(SeedEmail, result.Error.Message, StringComparison.Ordinal);
            Assert.Empty(context.UserStore.Users);
            Assert.Empty(context.RoleStore.Roles);
            Assert.Empty(context.AuditWriter.Entries);
            Assert.Equal(0, context.UnitOfWork.BeginCount);
            Assert.Equal(0, context.UnitOfWork.CommitCount);
            var capturedLogText = CapturedLogText(context);
            Assert.DoesNotContain(password, capturedLogText, StringComparison.Ordinal);
            Assert.DoesNotContain(SeedEmail, capturedLogText, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ABlankSeedPassword_FailsWithTheSafeMessage_AndCreatesNothing(string? password)
        {
            var context = CreateContext(new PlatformSeedOptions
            {
                SuperAdminEmail = SeedEmail,
                SuperAdminPassword = password,
            });

            var result = await context.Seeder.SeedAsync(CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(PlatformSeedErrors.InvalidConfigurationCode, result.Error!.Code);
            Assert.Equal(PlatformSeedErrors.InvalidConfigurationMessage, result.Error.Message);
            Assert.DoesNotContain(SeedEmail, result.Error.Message, StringComparison.Ordinal);
            Assert.Empty(context.UserStore.Users);
            Assert.Empty(context.RoleStore.Roles);
            Assert.Empty(context.AuditWriter.Entries);
            Assert.Equal(0, context.UnitOfWork.BeginCount);
            Assert.Equal(0, context.UnitOfWork.CommitCount);
            Assert.DoesNotContain(SeedEmail, CapturedLogText(context), StringComparison.Ordinal);
        }

        [Fact]
        public async Task CapturedLogs_NeverContainTheEmailOrPassword_AcrossEveryOutcome()
        {
            var createdContext = CreateContext(new PlatformSeedOptions
            {
                SuperAdminEmail = SeedEmail,
                SuperAdminPassword = SeedPassword,
            });
            await createdContext.Seeder.SeedAsync(CancellationToken.None);
            await createdContext.Seeder.SeedAsync(CancellationToken.None);

            var weakContext = CreateContext(new PlatformSeedOptions
            {
                SuperAdminEmail = SeedEmail,
                SuperAdminPassword = "weakpassword1!",
            });
            await weakContext.Seeder.SeedAsync(CancellationToken.None);

            var skippedContext = CreateContext(new PlatformSeedOptions
            {
                SuperAdminEmail = "",
                SuperAdminPassword = SeedPassword,
            });
            await skippedContext.Seeder.SeedAsync(CancellationToken.None);

            var capturedLogText = string.Join(
                "\n",
                new[]
                {
                    CapturedLogText(createdContext),
                    CapturedLogText(weakContext),
                    CapturedLogText(skippedContext),
                });

            Assert.NotEmpty(capturedLogText);
            Assert.DoesNotContain(SeedEmail, capturedLogText, StringComparison.Ordinal);
            Assert.DoesNotContain(SeedPassword, capturedLogText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AnExistingRole_IsReused_InsteadOfBeingDuplicated()
        {
            var existingRole = new PlatformAdminRole(PlatformAdminRoles.SuperAdmin)
            {
                Id = Guid.CreateVersion7(),
                NormalizedName = PlatformAdminRoles.SuperAdmin.ToUpperInvariant(),
                CreatedAt = FixedNow,
                UpdatedAt = FixedNow,
            };
            var roleStore = new FakePlatformAdminRoleStore();
            roleStore.Roles.Add(existingRole);
            var context = CreateContext(
                new PlatformSeedOptions
                {
                    SuperAdminEmail = SeedEmail,
                    SuperAdminPassword = SeedPassword,
                },
                roleStore: roleStore);

            var result = await context.Seeder.SeedAsync(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(SeedOutcome.Created, result.Value);
            Assert.Same(existingRole, Assert.Single(roleStore.Roles));
            Assert.Single(context.UserStore.Users);
            Assert.Single(context.AuditWriter.Entries);
        }

        private sealed record SeedTestContext(
            SuperAdminSeeder Seeder,
            UserManager<PlatformAdminUser> UserManager,
            FakePlatformAdminUserStore UserStore,
            FakePlatformAdminRoleStore RoleStore,
            RecordingSuperAdminAuditWriter AuditWriter,
            RecordingSeedUnitOfWork UnitOfWork,
            FakeLogger<SuperAdminSeeder> Logger);
    }

    internal sealed class RecordingSuperAdminAuditWriter : IPlatformAuditWriter
    {
        public List<PlatformAuditEntry> Entries { get; } = new();

        public void Write(PlatformAuditEntry entry)
        {
            Entries.Add(entry);
        }
    }

    internal sealed class RecordingSeedUnitOfWork : IMasterUnitOfWork
    {
        public int BeginCount { get; private set; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            BeginCount++;

            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            CommitCount++;

            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            RollbackCount++;

            return Task.CompletedTask;
        }
    }

    internal sealed class FakePlatformAdminRoleStore : IRoleStore<PlatformAdminRole>
    {
        public List<PlatformAdminRole> Roles { get; } = new();

        public Task<IdentityResult> CreateAsync(PlatformAdminRole role, CancellationToken cancellationToken)
        {
            Roles.Add(role);

            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(PlatformAdminRole role, CancellationToken cancellationToken)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(PlatformAdminRole role, CancellationToken cancellationToken)
        {
            Roles.Remove(role);

            return Task.FromResult(IdentityResult.Success);
        }

        public void Dispose()
        {
        }

        public Task<string> GetRoleIdAsync(PlatformAdminRole role, CancellationToken cancellationToken)
        {
            return Task.FromResult(role.Id.ToString());
        }

        public Task<string?> GetRoleNameAsync(PlatformAdminRole role, CancellationToken cancellationToken)
        {
            return Task.FromResult(role.Name);
        }

        public Task SetRoleNameAsync(PlatformAdminRole role, string? roleName, CancellationToken cancellationToken)
        {
            role.Name = roleName;

            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedRoleNameAsync(PlatformAdminRole role, CancellationToken cancellationToken)
        {
            return Task.FromResult(role.NormalizedName);
        }

        public Task SetNormalizedRoleNameAsync(PlatformAdminRole role, string? normalizedName, CancellationToken cancellationToken)
        {
            role.NormalizedName = normalizedName;

            return Task.CompletedTask;
        }

        public Task<PlatformAdminRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Guid.TryParse(roleId, out var id) ? Roles.FirstOrDefault(role => role.Id == id) : null);
        }

        public Task<PlatformAdminRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Roles.FirstOrDefault(role => string.Equals(role.NormalizedName, normalizedRoleName, StringComparison.Ordinal)));
        }
    }
}
