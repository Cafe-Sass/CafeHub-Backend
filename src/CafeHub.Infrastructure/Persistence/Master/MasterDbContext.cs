using CafeHub.Domain.Identity;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Provisioning;
using CafeHub.Domain.Subscriptions;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Master.Audit;
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
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<PlatformAuditRecord> PlatformAuditRecords => Set<PlatformAuditRecord>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
        public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();
        public DbSet<OnboardingRequest> OnboardingRequests => Set<OnboardingRequest>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<SubscriptionPeriod> SubscriptionPeriods => Set<SubscriptionPeriod>();
        public DbSet<PlatformPaymentTransaction> PlatformPaymentTransactions => Set<PlatformPaymentTransaction>();
        public DbSet<global::CafeHub.Domain.Tenants.Tenant> Tenants => Set<global::CafeHub.Domain.Tenants.Tenant>();
        public DbSet<global::CafeHub.Domain.Tenants.TenantDatabase> TenantDatabases => Set<global::CafeHub.Domain.Tenants.TenantDatabase>();
        public DbSet<global::CafeHub.Domain.Provisioning.ProvisioningJob> ProvisioningJobs => Set<global::CafeHub.Domain.Provisioning.ProvisioningJob>();
        public DbSet<global::CafeHub.Domain.Provisioning.ProvisioningJobAttempt> ProvisioningJobAttempts => Set<global::CafeHub.Domain.Provisioning.ProvisioningJobAttempt>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasterDbContext).Assembly);
        }
    }
}
