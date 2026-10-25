using CafeHub.Application.Abstractions.Results;
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
    public sealed class AdminAuthenticatorTests
    {
        private const string KnownEmail = "active.admin@cafehub.app";
        private const string KnownPassword = "known-password-1a2b3c";
        private const string UnknownEmail = "ghost.admin@cafehub.app";
        private const string InactiveEmail = "inactive.admin@cafehub.app";
        private const string LockedEmail = "locked.admin@cafehub.app";

        private readonly RecordingPasswordHasher passwordHasher = new();

        private static bool IsSafeInvalidCredentials(Error error)
        {
            return error.Code == AuthErrors.InvalidCredentialsCode
                && error.Message == AuthErrors.InvalidCredentialsMessage
                && error.Type == ErrorType.Unauthorized;
        }

        private PlatformAdminUser CreateAdminUser(
            string email,
            string? password = null,
            bool isActive = true,
            int failedAttempts = 0,
            DateTimeOffset? lockedUntil = null)
        {
            var user = new PlatformAdminUser(email)
            {
                Id = Guid.NewGuid(),
                Email = email,
                NormalizedEmail = PlatformAdminEmail.Normalize(email),
                SecurityStamp = Guid.NewGuid().ToString("N"),
            };

            if (password is not null)
            {
                user.PasswordHash = passwordHasher.HashPassword(user, password);
            }

            user.AccessFailedCount = failedAttempts;
            user.LockoutEnd = lockedUntil;

            if (!isActive)
            {
                user.IsActive = false;
            }

            return user;
        }

        private UserManager<PlatformAdminUser> CreateUserManager(
            FakePlatformAdminUserStore store,
            int maxFailedAccessAttempts,
            int lockoutMinutes)
        {
            var options = new IdentityOptions();
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = maxFailedAccessAttempts;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockoutMinutes);

            return new UserManager<PlatformAdminUser>(
                store,
                Options.Create(options),
                passwordHasher,
                Array.Empty<IUserValidator<PlatformAdminUser>>(),
                Array.Empty<IPasswordValidator<PlatformAdminUser>>(),
                new PlatformAdminLookupNormalizer(),
                new IdentityErrorDescriber(),
                new ServiceCollection().BuildServiceProvider(),
                NullLogger<UserManager<PlatformAdminUser>>.Instance);
        }

        private AdminAuthenticator CreateAuthenticator(
            FakePlatformAdminUserStore store,
            int maxFailedAccessAttempts = IdentityLockoutOptions.DefaultMaxFailedAccessAttempts,
            int lockoutMinutes = IdentityLockoutOptions.DefaultLockoutMinutes,
            DateTimeOffset? now = null)
        {
            var userManager = CreateUserManager(store, maxFailedAccessAttempts, lockoutMinutes);
            var timeProvider = new AdjustableTimeProvider { UtcNow = now ?? DateTimeOffset.UtcNow };

            return new AdminAuthenticator(userManager, passwordHasher, timeProvider);
        }

        [Fact]
        public async Task ValidCredentials_ReturnTheAdminWithTheStoredRoles()
        {
            var user = CreateAdminUser(KnownEmail, KnownPassword);
            var store = new FakePlatformAdminUserStore(user);
            store.AssignRole(user.Id, PlatformAdminRoles.SuperAdmin);
            var authenticator = CreateAuthenticator(store);

            var result = await authenticator.CheckCredentialsAsync(KnownEmail, KnownPassword, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(user.Id, result.Value!.Id);
            Assert.Equal(KnownEmail, result.Value.Email);
            Assert.Equal(new[] { PlatformAdminRoles.SuperAdmin }, result.Value.Roles);
        }

        [Fact]
        public async Task AnEmailWithDifferentCaseAndWhitespace_StillFindsTheAdmin_ThroughTheRepositoryEmailDecision()
        {
            var user = CreateAdminUser(KnownEmail, KnownPassword);
            var store = new FakePlatformAdminUserStore(user);
            var authenticator = CreateAuthenticator(store);

            var result = await authenticator.CheckCredentialsAsync("  Active.Admin@CafeHub.App ", KnownPassword, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(user.Id, result.Value!.Id);
        }

        [Fact]
        public async Task ValidCredentials_ResetTheFailedAttemptCount()
        {
            var user = CreateAdminUser(KnownEmail, KnownPassword, failedAttempts: 2);
            var store = new FakePlatformAdminUserStore(user);
            var authenticator = CreateAuthenticator(store);
            Assert.Equal(2, user.AccessFailedCount);

            var result = await authenticator.CheckCredentialsAsync(KnownEmail, KnownPassword, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, user.AccessFailedCount);
        }

        [Fact]
        public async Task AnUnknownEmail_IsRejectedWithTheSafeInvalidCredentialsError()
        {
            var store = new FakePlatformAdminUserStore();
            var authenticator = CreateAuthenticator(store);

            var result = await authenticator.CheckCredentialsAsync(UnknownEmail, KnownPassword, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.NotNull(result.Error);
            Assert.True(IsSafeInvalidCredentials(result.Error!));
        }

        [Fact]
        public async Task AnUnknownEmail_IsRejected_AfterOneVerificationAgainstTheDummyHash()
        {
            var knownUser = CreateAdminUser(KnownEmail, KnownPassword);
            var store = new FakePlatformAdminUserStore(knownUser);
            var authenticator = CreateAuthenticator(store);

            var result = await authenticator.CheckCredentialsAsync(UnknownEmail, KnownPassword, CancellationToken.None);

            Assert.True(result.IsFailure);
            var verification = Assert.Single(passwordHasher.Verifications);
            Assert.Equal(Guid.Empty, verification.User.Id);
            Assert.Contains(verification.Hash, passwordHasher.CreatedHashes);
            Assert.NotEqual(knownUser.PasswordHash, verification.Hash);
            Assert.Equal(KnownPassword, verification.Password);
            Assert.Equal(0, store.UpdateCount);
        }

        [Fact]
        public async Task AWrongPassword_FailsOneVerification_IncrementsTheFailedCount_AndCarriesTheAdminIdInternally()
        {
            const string submittedPassword = "definitely-not-the-password";
            var user = CreateAdminUser(KnownEmail, KnownPassword);
            var store = new FakePlatformAdminUserStore(user);
            var authenticator = CreateAuthenticator(store);

            var result = await authenticator.CheckCredentialsAsync(KnownEmail, submittedPassword, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.NotNull(result.Error);
            Assert.True(IsSafeInvalidCredentials(result.Error!));
            var rejected = Assert.IsType<RejectedAdminCredentials>(result.Error);
            Assert.Equal(user.Id, rejected.AdministratorId);
            var verification = Assert.Single(passwordHasher.Verifications);
            Assert.Same(user, verification.User);
            Assert.Equal(user.PasswordHash, verification.Hash);
            Assert.Equal(submittedPassword, verification.Password);
            Assert.Equal(1, user.AccessFailedCount);
        }

        [Fact]
        public async Task RepeatedWrongPasswords_CountEveryAttempt_AndLockTheAccountAfterTheConfiguredThreshold()
        {
            var user = CreateAdminUser(KnownEmail, KnownPassword);
            var store = new FakePlatformAdminUserStore(user);
            var authenticator = CreateAuthenticator(store);
            var attempts = IdentityLockoutOptions.DefaultMaxFailedAccessAttempts;
            var thresholdTime = DateTimeOffset.UtcNow;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                var rejection = await authenticator.CheckCredentialsAsync(KnownEmail, $"wrong-password-{attempt}", CancellationToken.None);
                Assert.True(rejection.IsFailure);
                Assert.IsType<RejectedAdminCredentials>(rejection.Error);

                if (attempt < attempts)
                {
                    Assert.Equal(attempt, user.AccessFailedCount);
                }
            }

            Assert.NotNull(user.LockoutEnd);
            Assert.True(user.LockoutEnd!.Value > thresholdTime);

            var locked = await authenticator.CheckCredentialsAsync(KnownEmail, "wrong-password-after-lockout", CancellationToken.None);

            Assert.True(locked.IsFailure);
            Assert.NotNull(locked.Error);
            Assert.True(IsSafeInvalidCredentials(locked.Error!));
            var lockout = Assert.IsType<LockedOutAdminCredentials>(locked.Error);
            Assert.Equal(user.Id, lockout.AdministratorId);
            Assert.Equal(
                Enumerable.Range(1, attempts).Select(attempt => $"wrong-password-{attempt}"),
                passwordHasher.Verifications.Select(verification => verification.Password));
        }

        [Fact]
        public async Task AnAlreadyLockedAccount_IsRejectedWithTheLockedOutCarrier_BeforeThePasswordIsVerified()
        {
            var lockedUntil = DateTimeOffset.UtcNow.AddMinutes(10);
            var user = CreateAdminUser(LockedEmail, KnownPassword, lockedUntil: lockedUntil);
            var store = new FakePlatformAdminUserStore(user);
            var authenticator = CreateAuthenticator(store);

            var result = await authenticator.CheckCredentialsAsync(LockedEmail, KnownPassword, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.NotNull(result.Error);
            Assert.True(IsSafeInvalidCredentials(result.Error!));
            var lockout = Assert.IsType<LockedOutAdminCredentials>(result.Error);
            Assert.Equal(user.Id, lockout.AdministratorId);
            Assert.Empty(passwordHasher.Verifications);
            Assert.Equal(0, user.AccessFailedCount);
        }

        [Fact]
        public async Task AnInactiveAdmin_IsRejectedWithTheRejectedCarrier_AfterOnlyTheDummyVerification()
        {
            var user = CreateAdminUser(InactiveEmail, KnownPassword, isActive: false);
            var store = new FakePlatformAdminUserStore(user);
            var authenticator = CreateAuthenticator(store);

            var result = await authenticator.CheckCredentialsAsync(InactiveEmail, KnownPassword, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.NotNull(result.Error);
            Assert.True(IsSafeInvalidCredentials(result.Error!));
            var rejected = Assert.IsType<RejectedAdminCredentials>(result.Error);
            Assert.Equal(user.Id, rejected.AdministratorId);
            var verification = Assert.Single(passwordHasher.Verifications);
            Assert.Equal(Guid.Empty, verification.User.Id);
            Assert.Contains(verification.Hash, passwordHasher.CreatedHashes);
            Assert.NotEqual(user.PasswordHash, verification.Hash);
            Assert.Equal(KnownPassword, verification.Password);
            Assert.Equal(0, user.AccessFailedCount);
            Assert.Equal(0, store.UpdateCount);
        }

        [Fact]
        public async Task EveryRejection_PresentsTheIdenticalClientVisibleError()
        {
            var knownUser = CreateAdminUser(KnownEmail, KnownPassword);
            var inactiveUser = CreateAdminUser(InactiveEmail, KnownPassword, isActive: false);
            var lockedUser = CreateAdminUser(LockedEmail, KnownPassword, lockedUntil: DateTimeOffset.UtcNow.AddMinutes(10));
            var store = new FakePlatformAdminUserStore(knownUser, inactiveUser, lockedUser);
            var authenticator = CreateAuthenticator(store);

            var unknown = await authenticator.CheckCredentialsAsync(UnknownEmail, KnownPassword, CancellationToken.None);
            var wrong = await authenticator.CheckCredentialsAsync(KnownEmail, "definitely-not-the-password", CancellationToken.None);
            var inactive = await authenticator.CheckCredentialsAsync(InactiveEmail, KnownPassword, CancellationToken.None);
            var locked = await authenticator.CheckCredentialsAsync(LockedEmail, KnownPassword, CancellationToken.None);

            foreach (var error in new[] { unknown.Error, wrong.Error, inactive.Error, locked.Error })
            {
                Assert.NotNull(error);
                Assert.True(IsSafeInvalidCredentials(error!));
            }

            Assert.IsNotType<RejectedAdminCredentials>(unknown.Error!);
            Assert.IsNotType<LockedOutAdminCredentials>(unknown.Error!);
            Assert.IsType<RejectedAdminCredentials>(wrong.Error!);
            Assert.IsType<RejectedAdminCredentials>(inactive.Error!);
            Assert.IsType<LockedOutAdminCredentials>(locked.Error!);
        }
    }

    internal sealed record PasswordVerificationAttempt(PlatformAdminUser User, string Hash, string Password);

    internal sealed class RecordingPasswordHasher : IPasswordHasher<PlatformAdminUser>
    {
        private readonly PasswordHasher<PlatformAdminUser> inner = new();

        public List<string> CreatedHashes { get; } = new();

        public List<PasswordVerificationAttempt> Verifications { get; } = new();

        public string HashPassword(PlatformAdminUser user, string password)
        {
            var hash = inner.HashPassword(user, password);
            CreatedHashes.Add(hash);

            return hash;
        }

        public PasswordVerificationResult VerifyHashedPassword(PlatformAdminUser user, string hashedPassword, string providedPassword)
        {
            Verifications.Add(new PasswordVerificationAttempt(user, hashedPassword, providedPassword));

            return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    internal sealed class AdjustableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; }

        public override DateTimeOffset GetUtcNow()
        {
            return UtcNow;
        }
    }

    internal sealed class FakePlatformAdminUserStore :
        IUserEmailStore<PlatformAdminUser>,
        IUserPasswordStore<PlatformAdminUser>,
        IUserLockoutStore<PlatformAdminUser>,
        IUserRoleStore<PlatformAdminUser>
    {
        public FakePlatformAdminUserStore(params PlatformAdminUser[] users)
        {
            Users = users.ToList();
        }

        public List<PlatformAdminUser> Users { get; }

        public int UpdateCount { get; private set; }

        private Dictionary<Guid, List<string>> RolesByUserId { get; } = new();

        public void AssignRole(Guid userId, string roleName)
        {
            RolesOf(userId).Add(roleName);
        }

        public void Dispose()
        {
        }

        public Task<string> GetUserIdAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Id.ToString());
        }

        public Task<string?> GetUserNameAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.UserName);
        }

        public Task SetUserNameAsync(PlatformAdminUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;

            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.NormalizedUserName);
        }

        public Task SetNormalizedUserNameAsync(PlatformAdminUser user, string? normalizedUserName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedUserName;

            return Task.CompletedTask;
        }

        public Task<IdentityResult> CreateAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            Users.Add(user);

            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            UpdateCount++;

            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            Users.Remove(user);

            return Task.FromResult(IdentityResult.Success);
        }

        public Task<PlatformAdminUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            var id = Guid.Parse(userId);

            return Task.FromResult(Users.FirstOrDefault(user => user.Id == id));
        }

        public Task<PlatformAdminUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            return Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.NormalizedUserName, normalizedUserName, StringComparison.Ordinal)));
        }

        public Task SetEmailAsync(PlatformAdminUser user, string? email, CancellationToken cancellationToken)
        {
            user.Email = email;

            return Task.CompletedTask;
        }

        public Task<string?> GetEmailAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Email);
        }

        public Task<bool> GetEmailConfirmedAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.EmailConfirmed);
        }

        public Task SetEmailConfirmedAsync(PlatformAdminUser user, bool confirmed, CancellationToken cancellationToken)
        {
            user.EmailConfirmed = confirmed;

            return Task.CompletedTask;
        }

        public Task<PlatformAdminUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Users.FirstOrDefault(user => string.Equals(user.NormalizedEmail, normalizedEmail, StringComparison.Ordinal)));
        }

        public Task<string?> GetNormalizedEmailAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.NormalizedEmail);
        }

        public Task SetNormalizedEmailAsync(PlatformAdminUser user, string? normalizedEmail, CancellationToken cancellationToken)
        {
            user.NormalizedEmail = normalizedEmail;

            return Task.CompletedTask;
        }

        public Task SetPasswordHashAsync(PlatformAdminUser user, string? passwordHash, CancellationToken cancellationToken)
        {
            user.PasswordHash = passwordHash;

            return Task.CompletedTask;
        }

        public Task<string?> GetPasswordHashAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.PasswordHash is not null);
        }

        public Task<DateTimeOffset?> GetLockoutEndDateAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.LockoutEnd);
        }

        public Task SetLockoutEndDateAsync(PlatformAdminUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
        {
            user.LockoutEnd = lockoutEnd;

            return Task.CompletedTask;
        }

        public Task<int> IncrementAccessFailedCountAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            user.AccessFailedCount++;

            return Task.FromResult(user.AccessFailedCount);
        }

        public Task ResetAccessFailedCountAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            user.AccessFailedCount = 0;

            return Task.CompletedTask;
        }

        public Task<int> GetAccessFailedCountAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.AccessFailedCount);
        }

        public Task<bool> GetLockoutEnabledAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.LockoutEnabled);
        }

        public Task SetLockoutEnabledAsync(PlatformAdminUser user, bool enabled, CancellationToken cancellationToken)
        {
            user.LockoutEnabled = enabled;

            return Task.CompletedTask;
        }

        public Task AddToRoleAsync(PlatformAdminUser user, string roleName, CancellationToken cancellationToken)
        {
            RolesOf(user.Id).Add(roleName);

            return Task.CompletedTask;
        }

        public Task RemoveFromRoleAsync(PlatformAdminUser user, string roleName, CancellationToken cancellationToken)
        {
            RolesOf(user.Id).Remove(roleName);

            return Task.CompletedTask;
        }

        public Task<IList<string>> GetRolesAsync(PlatformAdminUser user, CancellationToken cancellationToken)
        {
            IList<string> roles = RolesOf(user.Id).ToList();

            return Task.FromResult(roles);
        }

        public Task<bool> IsInRoleAsync(PlatformAdminUser user, string roleName, CancellationToken cancellationToken)
        {
            return Task.FromResult(RolesOf(user.Id).Contains(roleName));
        }

        public Task<IList<PlatformAdminUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        {
            IList<PlatformAdminUser> users = Users
                .Where(user => RolesOf(user.Id).Contains(roleName))
                .ToList();

            return Task.FromResult(users);
        }

        private List<string> RolesOf(Guid userId)
        {
            if (!RolesByUserId.TryGetValue(userId, out var roles))
            {
                roles = new List<string>();
                RolesByUserId[userId] = roles;
            }

            return roles;
        }
    }
}
