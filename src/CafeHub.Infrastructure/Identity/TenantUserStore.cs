using System.Security.Claims;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Identity
{
    internal sealed class TenantUserStore : IUserStore<TenantUser>, IUserEmailStore<TenantUser>, IUserPasswordStore<TenantUser>, IUserSecurityStampStore<TenantUser>, IUserLockoutStore<TenantUser>, IUserRoleStore<TenantUser>, IUserClaimStore<TenantUser>
    {
        private readonly TenantDbContext context;
        private readonly TimeProvider timeProvider;

        public TenantUserStore(TenantDbContext context, TimeProvider timeProvider)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public void Dispose()
        {
        }

        public async Task<IdentityResult> CreateAsync(TenantUser user, CancellationToken cancellationToken)
        {
            context.TenantUsers.Add(user);
            return await SaveAsync(cancellationToken);
        }

        public async Task<IdentityResult> UpdateAsync(TenantUser user, CancellationToken cancellationToken)
        {
            context.Entry(user).Property(nameof(TenantUser.ConcurrencyStamp)).CurrentValue = Guid.NewGuid().ToString("N");
            context.Entry(user).Property(nameof(TenantUser.UpdatedAt)).CurrentValue = timeProvider.GetUtcNow();
            return await SaveAsync(cancellationToken);
        }

        public async Task<IdentityResult> DeleteAsync(TenantUser user, CancellationToken cancellationToken)
        {
            context.Entry(user).Property(nameof(TenantUser.DeletedAt)).CurrentValue = timeProvider.GetUtcNow();
            context.Entry(user).Property(nameof(TenantUser.UpdatedAt)).CurrentValue = timeProvider.GetUtcNow();
            return await SaveAsync(cancellationToken);
        }

        public Task<string> GetUserIdAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
        public Task<string?> GetUserNameAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName);
        public Task SetUserNameAsync(TenantUser user, string? userName, CancellationToken cancellationToken) => SetAsync(user, nameof(TenantUser.UserName), userName);
        public Task<string?> GetNormalizedUserNameAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(TenantUser user, string? normalizedName, CancellationToken cancellationToken) => SetAsync(user, nameof(TenantUser.NormalizedUserName), normalizedName?.ToLowerInvariant() ?? string.Empty);
        public Task<TenantUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Guid.TryParse(userId, out var id) ? context.TenantUsers.SingleOrDefaultAsync(user => user.Id == id, cancellationToken) : Task.FromResult<TenantUser?>(null);
        public Task<TenantUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => context.TenantUsers.SingleOrDefaultAsync(user => user.NormalizedUserName == normalizedUserName.ToLowerInvariant(), cancellationToken);

        public Task SetEmailAsync(TenantUser user, string? email, CancellationToken cancellationToken) => SetAsync(user, nameof(TenantUser.Email), email);
        public Task<string?> GetEmailAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);
        public Task SetNormalizedEmailAsync(TenantUser user, string? normalizedEmail, CancellationToken cancellationToken) => SetAsync(user, nameof(TenantUser.NormalizedEmail), normalizedEmail?.ToLowerInvariant());
        public Task<string?> GetNormalizedEmailAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);
        public Task<bool> GetEmailConfirmedAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult(user.EmailConfirmed);
        public Task SetEmailConfirmedAsync(TenantUser user, bool confirmed, CancellationToken cancellationToken) => SetAsync(user, nameof(TenantUser.EmailConfirmed), confirmed);
        public Task<TenantUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => context.TenantUsers.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail.ToLowerInvariant(), cancellationToken);

        public Task SetPasswordHashAsync(TenantUser user, string? passwordHash, CancellationToken cancellationToken) => SetAsync(user, nameof(TenantUser.PasswordHash), passwordHash ?? string.Empty);
        public Task<string?> GetPasswordHashAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.PasswordHash);
        public Task<bool> HasPasswordAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
        public Task SetSecurityStampAsync(TenantUser user, string stamp, CancellationToken cancellationToken) => SetAsync(user, nameof(TenantUser.SecurityStamp), stamp);
        public Task<string?> GetSecurityStampAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.SecurityStamp);

        public Task<DateTimeOffset?> GetLockoutEndDateAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEndAt);
        public Task SetLockoutEndDateAsync(TenantUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken) => SetAsync(user, nameof(TenantUser.LockoutEndAt), lockoutEnd);
        public Task<int> IncrementAccessFailedCountAsync(TenantUser user, CancellationToken cancellationToken) => SetAndReturnAsync(user, nameof(TenantUser.AccessFailedCount), user.AccessFailedCount + 1, user.AccessFailedCount + 1);
        public Task ResetAccessFailedCountAsync(TenantUser user, CancellationToken cancellationToken) => SetAsync(user, nameof(TenantUser.AccessFailedCount), 0);
        public Task<int> GetAccessFailedCountAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult(user.AccessFailedCount);
        public Task<bool> GetLockoutEnabledAsync(TenantUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnabled);
        public Task SetLockoutEnabledAsync(TenantUser user, bool enabled, CancellationToken cancellationToken) => SetAsync(user, nameof(TenantUser.LockoutEnabled), enabled);

        public async Task AddToRoleAsync(TenantUser user, string normalizedRoleName, CancellationToken cancellationToken)
        {
            var normalized = normalizedRoleName.ToLowerInvariant();
            var role = await context.TenantRoles.SingleAsync(item => item.NormalizedName == normalized, cancellationToken);
            if (!await context.TenantUserRoles.AnyAsync(item => item.TenantUserId == user.Id && item.RoleId == role.Id, cancellationToken))
            {
                context.TenantUserRoles.Add(TenantUserRole.Create(Guid.CreateVersion7(), user.Id, role.Id, timeProvider.GetUtcNow()));
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task RemoveFromRoleAsync(TenantUser user, string normalizedRoleName, CancellationToken cancellationToken)
        {
            var normalized = normalizedRoleName.ToLowerInvariant();
            var role = await context.TenantRoles.SingleOrDefaultAsync(item => item.NormalizedName == normalized, cancellationToken);
            if (role is null) return;
            var memberships = await context.TenantUserRoles.Where(item => item.TenantUserId == user.Id && item.RoleId == role.Id).ToListAsync(cancellationToken);
            context.TenantUserRoles.RemoveRange(memberships);
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IList<string>> GetRolesAsync(TenantUser user, CancellationToken cancellationToken) => await context.TenantUserRoles.Where(link => link.TenantUserId == user.Id).Join(context.TenantRoles, link => link.RoleId, role => role.Id, (_, role) => role.Name).ToListAsync(cancellationToken);

        public async Task<bool> IsInRoleAsync(TenantUser user, string normalizedRoleName, CancellationToken cancellationToken) => await context.TenantUserRoles.Where(link => link.TenantUserId == user.Id).Join(context.TenantRoles, link => link.RoleId, role => role.Id, (_, role) => role.NormalizedName).AnyAsync(name => name == normalizedRoleName.ToLowerInvariant(), cancellationToken);

        public async Task<IList<TenantUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            return await context.TenantUserRoles.Join(context.TenantRoles, link => link.RoleId, role => role.Id, (link, role) => new { link, role }).Where(item => item.role.NormalizedName == normalizedRoleName.ToLowerInvariant()).Join(context.TenantUsers, item => item.link.TenantUserId, user => user.Id, (_, user) => user).ToListAsync(cancellationToken);
        }

        public async Task<IList<Claim>> GetClaimsAsync(TenantUser user, CancellationToken cancellationToken) => await context.TenantUserClaims.Where(claim => claim.TenantUserId == user.Id).Select(claim => new Claim(claim.ClaimType, claim.ClaimValue ?? string.Empty)).ToListAsync(cancellationToken);

        public async Task AddClaimsAsync(TenantUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            foreach (var claim in claims) context.TenantUserClaims.Add(TenantUserClaim.Create(Guid.CreateVersion7(), user.Id, claim.Type, claim.Value, timeProvider.GetUtcNow()));
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task ReplaceClaimAsync(TenantUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
        {
            var oldClaims = await context.TenantUserClaims.Where(item => item.TenantUserId == user.Id && item.ClaimType == claim.Type && item.ClaimValue == claim.Value).ToListAsync(cancellationToken);
            context.TenantUserClaims.RemoveRange(oldClaims);
            context.TenantUserClaims.Add(TenantUserClaim.Create(Guid.CreateVersion7(), user.Id, newClaim.Type, newClaim.Value, timeProvider.GetUtcNow()));
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveClaimsAsync(TenantUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            foreach (var claim in claims)
            {
                var matches = await context.TenantUserClaims.Where(item => item.TenantUserId == user.Id && item.ClaimType == claim.Type && item.ClaimValue == claim.Value).ToListAsync(cancellationToken);
                context.TenantUserClaims.RemoveRange(matches);
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IList<TenantUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken) => await context.TenantUserClaims.Where(item => item.ClaimType == claim.Type && item.ClaimValue == claim.Value).Join(context.TenantUsers, item => item.TenantUserId, user => user.Id, (_, user) => user).ToListAsync(cancellationToken);

        private Task SetAsync<TValue>(TenantUser user, string propertyName, TValue value)
        {
            context.Entry(user).Property(propertyName).CurrentValue = value;
            return Task.CompletedTask;
        }

        private async Task<TValue> SetAndReturnAsync<TValue>(TenantUser user, string propertyName, TValue value, TValue result)
        {
            await SetAsync(user, propertyName, value);
            return result;
        }

        private async Task<IdentityResult> SaveAsync(CancellationToken cancellationToken)
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return IdentityResult.Success;
            }
            catch (DbUpdateConcurrencyException)
            {
                return IdentityResult.Failed(new IdentityError { Code = nameof(IdentityErrorDescriber.ConcurrencyFailure), Description = "A concurrent update was detected." });
            }
        }
    }
}
