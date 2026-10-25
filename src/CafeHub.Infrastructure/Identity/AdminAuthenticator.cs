using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Identity
{
    /// <summary>
    /// Authenticates Super Admin credentials over ASP.NET Core Identity. The framework's normalizer is the
    /// <see cref="PlatformAdminLookupNormalizer"/>, so find-by-email follows the repository email decision.
    /// An unknown email, a deactivated admin, or an account without a password hash is rejected after one
    /// verification pass against a per-instance dummy hash, so the response time does not reveal whether
    /// the account exists. Failed attempts go through <see cref="UserManager{PlatformAdminUser}.AccessFailedAsync"/>,
    /// which counts them and applies the configured lockout window; success resets the counter. Every
    /// rejection maps to the single safe <see cref="AuthErrors.InvalidCredentials"/> error, with the lockout
    /// signal staying internal to the audit-only error carriers.
    /// </summary>
    internal sealed class AdminAuthenticator : IAdminAuthenticator
    {
        private const string DummyPassword = "dummy-password-for-timing-parity";

        private readonly UserManager<PlatformAdminUser> userManager;
        private readonly IPasswordHasher<PlatformAdminUser> passwordHasher;
        private readonly TimeProvider timeProvider;
        private readonly PlatformAdminUser dummyAdminUser;
        private readonly string dummyPasswordHash;

        public AdminAuthenticator(
            UserManager<PlatformAdminUser> userManager,
            IPasswordHasher<PlatformAdminUser> passwordHasher,
            TimeProvider timeProvider)
        {
            this.userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            dummyAdminUser = CreateDummyAdminUser();
            dummyPasswordHash = this.passwordHasher.HashPassword(dummyAdminUser, DummyPassword);
        }

        public async Task<Result<AuthenticatedAdmin>> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken)
        {
            if (email is null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            if (password is null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            var admin = await userManager.FindByEmailAsync(email);

            if (admin is null)
            {
                VerifyDummyPassword(password);

                return Result.Failure<AuthenticatedAdmin>(AuthErrors.InvalidCredentials());
            }

            if (!admin.IsActive || admin.PasswordHash is null)
            {
                VerifyDummyPassword(password);

                return Result.Failure<AuthenticatedAdmin>(RejectedAdminCredentials.Create(admin.Id));
            }

            if (admin.LockoutEnabled)
            {
                var lockoutEnd = await userManager.GetLockoutEndDateAsync(admin);
                if (lockoutEnd is { } lockedUntil && lockedUntil > timeProvider.GetUtcNow())
                {
                    return Result.Failure<AuthenticatedAdmin>(LockedOutAdminCredentials.Create(admin.Id));
                }
            }

            var passwordIsCorrect = await userManager.CheckPasswordAsync(admin, password);

            if (!passwordIsCorrect)
            {
                await userManager.AccessFailedAsync(admin);

                return Result.Failure<AuthenticatedAdmin>(RejectedAdminCredentials.Create(admin.Id));
            }

            await userManager.ResetAccessFailedCountAsync(admin);
            var roles = await userManager.GetRolesAsync(admin);

            return Result.Success(new AuthenticatedAdmin
            {
                Id = admin.Id,
                Email = admin.Email ?? string.Empty,
                Roles = roles.ToList(),
            });
        }

        private void VerifyDummyPassword(string password)
        {
            passwordHasher.VerifyHashedPassword(dummyAdminUser, dummyPasswordHash, password);
        }

        private static PlatformAdminUser CreateDummyAdminUser()
        {
            return new PlatformAdminUser
            {
                Id = Guid.Empty,
                SecurityStamp = Guid.NewGuid().ToString("N"),
            };
        }
    }
}
