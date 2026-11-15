using System.Security.Claims;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Roles;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Identity
{
    internal sealed partial class TenantRoleStore : IRoleStore<TenantRole>, IRoleClaimStore<TenantRole>, ITenantRoleAdministration
    {
        private readonly TenantDbContext context;
        private readonly TimeProvider timeProvider;
        private readonly ITenantAuditWriter auditWriter;

        public TenantRoleStore(TenantDbContext context, TimeProvider timeProvider, ITenantAuditWriter auditWriter)
        {
            this.context = context;
            this.timeProvider = timeProvider;
            this.auditWriter = auditWriter;
        }

        public void Dispose() { }
        public Task<IdentityResult> CreateAsync(TenantRole role, CancellationToken cancellationToken) => RoleWriteDisabled();
        public Task<IdentityResult> UpdateAsync(TenantRole role, CancellationToken cancellationToken) => RoleWriteDisabled();
        public Task<IdentityResult> DeleteAsync(TenantRole role, CancellationToken cancellationToken) => RoleWriteDisabled();
        public Task<string> GetRoleIdAsync(TenantRole role, CancellationToken cancellationToken) => Task.FromResult(role.Id.ToString());
        public Task<string?> GetRoleNameAsync(TenantRole role, CancellationToken cancellationToken) => Task.FromResult<string?>(role.Name);
        public Task SetRoleNameAsync(TenantRole role, string? roleName, CancellationToken cancellationToken) => throw new NotSupportedException("Use audited tenant role administration.");
        public Task<string?> GetNormalizedRoleNameAsync(TenantRole role, CancellationToken cancellationToken) => Task.FromResult<string?>(role.NormalizedName);
        public Task SetNormalizedRoleNameAsync(TenantRole role, string? normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException("Use audited tenant role administration.");
        public Task<TenantRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken) => Guid.TryParse(roleId, out var id) ? FindActiveRoleAsync(id, cancellationToken) : Task.FromResult<TenantRole?>(null);
        public Task<TenantRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken) => context.TenantRoles.SingleOrDefaultAsync(role => role.DeletedAt == null && role.NormalizedName == normalizedRoleName.Trim().ToLowerInvariant(), cancellationToken);
        public async Task<IList<Claim>> GetClaimsAsync(TenantRole role, CancellationToken cancellationToken) => await context.TenantRoleClaims.Where(claim => claim.RoleId == role.Id).Select(claim => new Claim(claim.ClaimType, claim.ClaimValue ?? string.Empty)).ToListAsync(cancellationToken);
        public async Task AddClaimAsync(TenantRole role, Claim claim, CancellationToken cancellationToken)
        {
            EnsureClaimWritable(role, claim);
            context.TenantRoleClaims.Add(TenantRoleClaim.Create(Guid.CreateVersion7(), role.Id, claim.Type, claim.Value, timeProvider.GetUtcNow()));
            await context.SaveChangesAsync(cancellationToken);
        }
        public async Task RemoveClaimAsync(TenantRole role, Claim claim, CancellationToken cancellationToken)
        {
            EnsureClaimWritable(role, claim);
            var rows = await context.TenantRoleClaims.Where(item => item.RoleId == role.Id && item.ClaimType == claim.Type && item.ClaimValue == claim.Value).ToListAsync(cancellationToken);
            context.TenantRoleClaims.RemoveRange(rows);
            await context.SaveChangesAsync(cancellationToken);
        }
        private static void EnsureClaimWritable(TenantRole role, Claim claim)
        {
            if (role.IsOwnerRole || role.DeletedAt.HasValue || claim.Type == TenantPermissions.ClaimType)
            {
                throw new NotSupportedException("Use audited tenant role administration.");
            }
        }

        private static Task<IdentityResult> RoleWriteDisabled() =>
            Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "UseTenantRoleAdministration",
                Description = "Use audited tenant role administration.",
            }));
    }
}
