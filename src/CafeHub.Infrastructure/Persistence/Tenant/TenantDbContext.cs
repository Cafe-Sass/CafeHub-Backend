using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Tenant
{
    public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
    {
        public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
        public DbSet<TenantRole> TenantRoles => Set<TenantRole>();
        public DbSet<TenantUserRole> TenantUserRoles => Set<TenantUserRole>();
        public DbSet<TenantUserClaim> TenantUserClaims => Set<TenantUserClaim>();
        public DbSet<TenantRoleClaim> TenantRoleClaims => Set<TenantRoleClaim>();
        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<CafeSettings> CafeSettings => Set<CafeSettings>();
        public DbSet<TenantAuditRecord> TenantAuditRecords => Set<TenantAuditRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(TenantDbContext).Assembly,
                type => type.Namespace == "CafeHub.Infrastructure.Persistence.Tenant.Configurations");
        }
    }
}
