using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Identity
{
    internal sealed class TenantUserAuthenticator : ITenantUserAuthenticator
    {
        private const string DummyPassword = "dummy-password-for-timing-parity";
        private readonly UserManager<TenantUser> userManager;
        private readonly IPasswordHasher<TenantUser> passwordHasher;
        private readonly TimeProvider timeProvider;
        private readonly TenantUser dummyUser;
        private readonly string dummyPasswordHash;
        public int DummyVerifications { get; private set; }

        public TenantUserAuthenticator(UserManager<TenantUser> userManager, IPasswordHasher<TenantUser> passwordHasher, TimeProvider timeProvider)
        {
            this.userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            dummyUser = TenantUser.Create(Guid.CreateVersion7(), TenantUserKind.Owner, "dummy-user", "dummy-user", "Dummy", "dummy@example.invalid", "dummy@example.invalid", null, "dummy-password-hash", Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), timeProvider.GetUtcNow());
            dummyPasswordHash = passwordHasher.HashPassword(dummyUser, DummyPassword);
        }

        public async Task<Result<AuthenticatedTenantUser>> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null || user.DeletedAt is not null || user.PasswordHash is null)
            {
                VerifyDummyPassword(password);
                return Result.Failure<AuthenticatedTenantUser>(AuthErrors.InvalidCredentials());
            }

            if (user.LockoutEnabled && await userManager.GetLockoutEndDateAsync(user) is { } until && until > timeProvider.GetUtcNow())
            {
                VerifyDummyPassword(password);
                return Result.Failure<AuthenticatedTenantUser>(AuthErrors.InvalidCredentials());
            }

            if (!await userManager.CheckPasswordAsync(user, password))
            {
                await userManager.AccessFailedAsync(user);
                return Result.Failure<AuthenticatedTenantUser>(AuthErrors.InvalidCredentials());
            }

            await userManager.ResetAccessFailedCountAsync(user);
            var roles = await userManager.GetRolesAsync(user);
            return Result.Success(new AuthenticatedTenantUser
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                UserKind = user.UserKind.ToString(),
                Roles = roles.ToList(),
            });
        }

        private void VerifyDummyPassword(string password)
        {
            DummyVerifications++;
            passwordHasher.VerifyHashedPassword(dummyUser, dummyPasswordHash, password);
        }
    }
}
