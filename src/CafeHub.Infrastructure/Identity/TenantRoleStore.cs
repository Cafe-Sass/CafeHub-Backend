using System.Security.Claims;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Identity
{
    internal sealed class TenantRoleStore : IRoleStore<TenantRole>, IRoleClaimStore<TenantRole>
    {
        private readonly TenantDbContext context;
        private readonly TimeProvider timeProvider;

        public TenantRoleStore(TenantDbContext context, TimeProvider timeProvider)
        {
            this.context = context;
            this.timeProvider = timeProvider;
        }

        public void Dispose() { }
        public async Task<IdentityResult> CreateAsync(TenantRole role, CancellationToken cancellationToken)
        {
            context.TenantRoles.Add(role);
            return await SaveChangesAsync(cancellationToken);
        }
        public Task<IdentityResult> UpdateAsync(TenantRole role, CancellationToken cancellationToken)
        {
            context.Entry(role).Property(nameof(TenantRole.ConcurrencyStamp)).CurrentValue = Guid.NewGuid().ToString("N");
            context.Entry(role).Property(nameof(TenantRole.UpdatedAt)).CurrentValue = timeProvider.GetUtcNow();
            return SaveChangesAsync(cancellationToken);
        }
        public async Task<IdentityResult> DeleteAsync(TenantRole role, CancellationToken cancellationToken)
        {
            context.TenantRoles.Remove(role);
            return await SaveChangesAsync(cancellationToken);
        }
        public Task<string> GetRoleIdAsync(TenantRole role, CancellationToken cancellationToken) => Task.FromResult(role.Id.ToString());
        public Task<string?> GetRoleNameAsync(TenantRole role, CancellationToken cancellationToken) => Task.FromResult<string?>(role.Name);
        public Task SetRoleNameAsync(TenantRole role, string? roleName, CancellationToken cancellationToken) => SetAsync(role, nameof(TenantRole.Name), roleName ?? string.Empty);
        public Task<string?> GetNormalizedRoleNameAsync(TenantRole role, CancellationToken cancellationToken) => Task.FromResult<string?>(role.NormalizedName);
        public Task SetNormalizedRoleNameAsync(TenantRole role, string? normalizedName, CancellationToken cancellationToken) => SetAsync(role, nameof(TenantRole.NormalizedName), normalizedName?.ToLowerInvariant() ?? string.Empty);
        public Task<TenantRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken) => Guid.TryParse(roleId, out var id) ? context.TenantRoles.SingleOrDefaultAsync(role => role.Id == id, cancellationToken) : Task.FromResult<TenantRole?>(null);
        public Task<TenantRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken) => context.TenantRoles.SingleOrDefaultAsync(role => role.NormalizedName == normalizedRoleName.ToLowerInvariant(), cancellationToken);
        public async Task<IList<Claim>> GetClaimsAsync(TenantRole role, CancellationToken cancellationToken) => await context.TenantRoleClaims.Where(claim => claim.RoleId == role.Id).Select(claim => new Claim(claim.ClaimType, claim.ClaimValue ?? string.Empty)).ToListAsync(cancellationToken);
        public async Task AddClaimAsync(TenantRole role, Claim claim, CancellationToken cancellationToken)
        {
            context.TenantRoleClaims.Add(TenantRoleClaim.Create(Guid.CreateVersion7(), role.Id, claim.Type, claim.Value, timeProvider.GetUtcNow()));
            await context.SaveChangesAsync(cancellationToken);
        }
        public async Task RemoveClaimAsync(TenantRole role, Claim claim, CancellationToken cancellationToken)
        {
            var rows = await context.TenantRoleClaims.Where(item => item.RoleId == role.Id && item.ClaimType == claim.Type && item.ClaimValue == claim.Value).ToListAsync(cancellationToken);
            context.TenantRoleClaims.RemoveRange(rows);
            await context.SaveChangesAsync(cancellationToken);
        }
        private Task SetAsync<TValue>(TenantRole role, string propertyName, TValue value)
        {
            context.Entry(role).Property(propertyName).CurrentValue = value;
            return Task.CompletedTask;
        }
        private async Task<IdentityResult> SaveChangesAsync(CancellationToken cancellationToken)
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
