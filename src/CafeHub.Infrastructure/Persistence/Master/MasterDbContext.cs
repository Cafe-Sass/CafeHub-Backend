using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master
{
    public sealed class MasterDbContext(DbContextOptions<MasterDbContext> options) : DbContext(options)
    {
        public DbSet<PlatformAdminUser> PlatformAdmins => Set<PlatformAdminUser>();
        public DbSet<PlatformAdminRole> PlatformAdminRoles => Set<PlatformAdminRole>();
        public DbSet<PlatformAdminUserRole> PlatformAdminUserRoles => Set<PlatformAdminUserRole>();
        public DbSet<PlatformAdminUserClaim> PlatformAdminUserClaims => Set<PlatformAdminUserClaim>();
        public DbSet<PlatformAdminRoleClaim> PlatformAdminRoleClaims => Set<PlatformAdminRoleClaim>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasterDbContext).Assembly);
        }
    }
}
