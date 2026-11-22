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
        public DbSet<Station> Stations => Set<Station>();
        public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
        public DbSet<ProductBranchStationSlot> ProductBranchStationSlots => Set<ProductBranchStationSlot>();
        public DbSet<ProductStationAssignment> ProductStationAssignments => Set<ProductStationAssignment>();
        public DbSet<ProductRoutingReadiness> ProductRoutingReadiness => Set<ProductRoutingReadiness>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductSize> ProductSizes => Set<ProductSize>();
        public DbSet<ProductAddon> ProductAddons => Set<ProductAddon>();
        public DbSet<ProductTag> ProductTags => Set<ProductTag>();
        public DbSet<BranchProductAvailability> BranchProductAvailability => Set<BranchProductAvailability>();
        public DbSet<BranchTable> BranchTables => Set<BranchTable>();
        public DbSet<TenantUserStation> TenantUserStations => Set<TenantUserStation>();
        public DbSet<CafeSettings> CafeSettings => Set<CafeSettings>();
        public DbSet<TenantAuditRecord> TenantAuditRecords => Set<TenantAuditRecord>();
        public DbSet<TenantRefreshToken> TenantRefreshTokens => Set<TenantRefreshToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(TenantDbContext).Assembly,
                type => type.Namespace == "CafeHub.Infrastructure.Persistence.Tenant.Configurations");
        }
    }
}
