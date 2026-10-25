using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class TenantConfiguration : IEntityTypeConfiguration<global::CafeHub.Domain.Tenants.Tenant>
    {
        public void Configure(EntityTypeBuilder<global::CafeHub.Domain.Tenants.Tenant> builder)
        {
            builder.ToTable("tenants", table =>
            {
                table.HasCheckConstraint("ck_tenants_slug_format", "slug ~ '^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$'");
                table.HasCheckConstraint("ck_tenants_slug_lowercase", "slug = lower(slug)");
                table.HasCheckConstraint("ck_tenants_slug_reserved", "slug NOT IN ('admin','api','t','www','app','static','assets','health','login','signup','support','platform')");
                table.HasCheckConstraint("ck_tenants_lifecycle_status", "lifecycle_status IN ('Provisioning', 'Active', 'Suspended', 'Terminated')");
            });

            builder.HasKey(tenant => tenant.Id).HasName("pk_tenants");
            builder.Property(tenant => tenant.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(tenant => tenant.OnboardingRequestId).HasColumnName("onboarding_request_id").HasColumnType("uuid").IsRequired();
            builder.Property(tenant => tenant.Slug).HasColumnName("slug").HasColumnType("text").IsRequired();
            builder.Property(tenant => tenant.LifecycleStatus).HasColumnName("lifecycle_status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(tenant => tenant.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(tenant => tenant.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(tenant => tenant.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(tenant => tenant.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(tenant => tenant.Slug)
                .IsUnique()
                .HasDatabaseName("uq_tenants_slug");
            builder.HasIndex(tenant => tenant.OnboardingRequestId)
                .IsUnique()
                .HasDatabaseName("uq_tenants_onboarding_request_id");

            builder.HasOne<OnboardingRequest>()
                .WithMany()
                .HasForeignKey(tenant => tenant.OnboardingRequestId)
                .HasConstraintName("fk_tenants_onboarding_request_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(tenant => tenant.CreatedBy)
                .HasConstraintName("fk_tenants_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(tenant => tenant.UpdatedBy)
                .HasConstraintName("fk_tenants_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
