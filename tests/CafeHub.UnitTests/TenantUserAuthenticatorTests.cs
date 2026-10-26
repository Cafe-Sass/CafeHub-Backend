using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CafeHub.UnitTests
{
    public sealed class TenantUserAuthenticatorTests
    {
        private const string Email = "owner@example.com";
        private const string Password = "known-password-1a2b3c";

        [Fact]
        public async Task ProvisionedOwnerHash_IsVerifiedByUserManagerHasher()
        {
            var owner = CreateUser(Password);
            var store = new FakeStore(owner);
            store.AssignRole(owner.Id, "Owner");
            var authenticator = CreateAuthenticator(store);

            var result = await authenticator.CheckCredentialsAsync(Email, Password, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(owner.Id, result.Value!.Id);
            Assert.Equal(new[] { "Owner" }, result.Value.Roles);
        }

        [Fact]
        public async Task FiveFailures_LockTheAccountAndSuccessResetsTheCounter()
        {
            var owner = CreateUser(Password);
            var store = new FakeStore(owner);
            var authenticator = CreateAuthenticator(store);

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var failed = await authenticator.CheckCredentialsAsync(Email, "wrong-password", CancellationToken.None);
                Assert.Equal(AuthErrors.InvalidCredentials(), failed.Error);
            }

            var locked = await authenticator.CheckCredentialsAsync(Email, Password, CancellationToken.None);
            Assert.Equal(AuthErrors.InvalidCredentials(), locked.Error);
            Assert.True(store.IsLocked(owner.Id));

            store.AdvanceLockout(owner.Id, DateTimeOffset.UtcNow.AddMinutes(-1));
            var success = await authenticator.CheckCredentialsAsync(Email, Password, CancellationToken.None);
            Assert.True(success.IsSuccess);
            Assert.Equal(0, store.FailedCount(owner.Id));
        }

        [Fact]
        public async Task UnknownEmail_UsesDummyHashAndReturnsTheGenericError()
        {
            var store = new FakeStore();
            var authenticator = CreateAuthenticator(store);

            var result = await authenticator.CheckCredentialsAsync("missing@example.com", Password, CancellationToken.None);

            Assert.Equal(AuthErrors.InvalidCredentials(), result.Error);
            Assert.Equal(1, authenticator.DummyVerifications);
        }

        [Fact]
        public async Task DeletedUser_IsRejectedWithTheSameErrorAsAnIncorrectPassword()
        {
            var deleted = CreateUser(Password);
            typeof(TenantUser).GetProperty(nameof(TenantUser.DeletedAt))!.SetValue(deleted, DateTimeOffset.UtcNow);
            var authenticator = CreateAuthenticator(new FakeStore(deleted));

            var deletedResult = await authenticator.CheckCredentialsAsync(Email, Password, CancellationToken.None);
            var incorrectPasswordResult = await authenticator.CheckCredentialsAsync(Email, "wrong-password", CancellationToken.None);

            Assert.Equal(incorrectPasswordResult.Error, deletedResult.Error);
            Assert.Equal(AuthErrors.InvalidCredentials(), deletedResult.Error);
        }

        private static TenantUser CreateUser(string password)
        {
            var hasher = new PasswordHasher<object>();
            var hash = hasher.HashPassword(new object(), password);
            return TenantUser.Create(Guid.NewGuid(), TenantUserKind.Owner, "owner-user", "owner-user", "Cafe Owner", Email, Email, null, hash, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
        }

        private static TenantUserAuthenticator CreateAuthenticator(FakeStore store)
        {
            var passwordHasher = new PasswordHasher<TenantUser>();
            var options = new IdentityOptions();
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            var manager = new UserManager<TenantUser>(store, Options.Create(options), passwordHasher, Array.Empty<IUserValidator<TenantUser>>(), Array.Empty<IPasswordValidator<TenantUser>>(), new TestNormalizer(), new IdentityErrorDescriber(), new ServiceCollection().BuildServiceProvider(), NullLogger<UserManager<TenantUser>>.Instance);
            return new TenantUserAuthenticator(manager, passwordHasher, TimeProvider.System);
        }

        private sealed class TestNormalizer : ILookupNormalizer
        {
            public string? NormalizeName(string? name) => name?.ToLowerInvariant();
            public string? NormalizeEmail(string? email) => email?.ToLowerInvariant();
        }

        private sealed class FakeStore : IUserEmailStore<TenantUser>, IUserPasswordStore<TenantUser>, IUserLockoutStore<TenantUser>, IUserRoleStore<TenantUser>
        {
            private readonly Dictionary<Guid, int> failed = new();
            private readonly Dictionary<Guid, DateTimeOffset?> lockout = new();
            private readonly Dictionary<Guid, List<string>> roles = new();
            public FakeStore(params TenantUser[] users) => Users = users.ToList();
            private List<TenantUser> Users { get; }
            public int FailedCount(Guid id) => failed.GetValueOrDefault(id);
            public bool IsLocked(Guid id) => lockout.TryGetValue(id, out var until) && until > DateTimeOffset.UtcNow;
            public void AdvanceLockout(Guid id, DateTimeOffset value) => lockout[id] = value;
            public void AssignRole(Guid id, string role) => roles[id] = new List<string> { role };
            public void Dispose() { }
            public Task<string> GetUserIdAsync(TenantUser user, CancellationToken ct) => Task.FromResult(user.Id.ToString());
            public Task<string?> GetUserNameAsync(TenantUser user, CancellationToken ct) => Task.FromResult<string?>(user.UserName);
            public Task SetUserNameAsync(TenantUser user, string? name, CancellationToken ct) => Task.CompletedTask;
            public Task<string?> GetNormalizedUserNameAsync(TenantUser user, CancellationToken ct) => Task.FromResult<string?>(user.NormalizedUserName);
            public Task SetNormalizedUserNameAsync(TenantUser user, string? name, CancellationToken ct) => Task.CompletedTask;
            public Task<IdentityResult> CreateAsync(TenantUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
            public Task<IdentityResult> UpdateAsync(TenantUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
            public Task<IdentityResult> DeleteAsync(TenantUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
            public Task<TenantUser?> FindByIdAsync(string id, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(user => user.Id.ToString() == id));
            public Task<TenantUser?> FindByNameAsync(string name, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(user => user.NormalizedUserName == name));
            public Task SetEmailAsync(TenantUser user, string? email, CancellationToken ct) => Task.CompletedTask;
            public Task<string?> GetEmailAsync(TenantUser user, CancellationToken ct) => Task.FromResult(user.Email);
            public Task<bool> GetEmailConfirmedAsync(TenantUser user, CancellationToken ct) => Task.FromResult(user.EmailConfirmed);
            public Task SetEmailConfirmedAsync(TenantUser user, bool confirmed, CancellationToken ct) => Task.CompletedTask;
            public Task<TenantUser?> FindByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(user => user.NormalizedEmail == email));
            public Task<string?> GetNormalizedEmailAsync(TenantUser user, CancellationToken ct) => Task.FromResult(user.NormalizedEmail);
            public Task SetNormalizedEmailAsync(TenantUser user, string? email, CancellationToken ct) => Task.CompletedTask;
            public Task SetPasswordHashAsync(TenantUser user, string? hash, CancellationToken ct) => Task.CompletedTask;
            public Task<string?> GetPasswordHashAsync(TenantUser user, CancellationToken ct) => Task.FromResult<string?>(user.PasswordHash);
            public Task<bool> HasPasswordAsync(TenantUser user, CancellationToken ct) => Task.FromResult(true);
            public Task<DateTimeOffset?> GetLockoutEndDateAsync(TenantUser user, CancellationToken ct) => Task.FromResult(lockout.GetValueOrDefault(user.Id));
            public Task SetLockoutEndDateAsync(TenantUser user, DateTimeOffset? end, CancellationToken ct) { lockout[user.Id] = end; return Task.CompletedTask; }
            public Task<int> IncrementAccessFailedCountAsync(TenantUser user, CancellationToken ct) { failed[user.Id] = FailedCount(user.Id) + 1; return Task.FromResult(failed[user.Id]); }
            public Task ResetAccessFailedCountAsync(TenantUser user, CancellationToken ct) { failed[user.Id] = 0; return Task.CompletedTask; }
            public Task<int> GetAccessFailedCountAsync(TenantUser user, CancellationToken ct) => Task.FromResult(FailedCount(user.Id));
            public Task<bool> GetLockoutEnabledAsync(TenantUser user, CancellationToken ct) => Task.FromResult(true);
            public Task SetLockoutEnabledAsync(TenantUser user, bool enabled, CancellationToken ct) => Task.CompletedTask;
            public Task AddToRoleAsync(TenantUser user, string role, CancellationToken ct) { roles[user.Id] = new List<string> { role }; return Task.CompletedTask; }
            public Task RemoveFromRoleAsync(TenantUser user, string role, CancellationToken ct) => Task.CompletedTask;
            public Task<IList<string>> GetRolesAsync(TenantUser user, CancellationToken ct) => Task.FromResult<IList<string>>(roles.GetValueOrDefault(user.Id, new List<string>()));
            public Task<bool> IsInRoleAsync(TenantUser user, string role, CancellationToken ct) => Task.FromResult(roles.GetValueOrDefault(user.Id, new List<string>()).Contains(role));
            public Task<IList<TenantUser>> GetUsersInRoleAsync(string role, CancellationToken ct) => Task.FromResult<IList<TenantUser>>(Users.Where(user => roles.GetValueOrDefault(user.Id, new List<string>()).Contains(role)).ToList());
        }
    }
}
