using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class ProvisioningJobConfiguration : IEntityTypeConfiguration<global::CafeHub.Domain.Provisioning.ProvisioningJob>
    {
        public void Configure(EntityTypeBuilder<global::CafeHub.Domain.Provisioning.ProvisioningJob> builder)
        {
            builder.ToTable("provisioning_jobs", table =>
            {
                table.HasCheckConstraint("ck_provisioning_jobs_status", "status IN ('Pending', 'Running', 'Succeeded', 'Failed')");
            });

            builder.HasKey(job => job.Id).HasName("pk_provisioning_jobs");
            builder.Property(job => job.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(job => job.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
            builder.Property(job => job.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(job => job.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(job => job.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(job => job.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(job => job.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(job => job.TenantId)
                .IsUnique()
                .HasDatabaseName("uq_provisioning_jobs_tenant_id");

            builder.HasOne<global::CafeHub.Domain.Tenants.Tenant>()
                .WithMany()
                .HasForeignKey(job => job.TenantId)
                .HasConstraintName("fk_provisioning_jobs_tenant_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<global::CafeHub.Infrastructure.Persistence.Master.Identity.PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(job => job.CreatedBy)
                .HasConstraintName("fk_provisioning_jobs_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<global::CafeHub.Infrastructure.Persistence.Master.Identity.PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(job => job.UpdatedBy)
                .HasConstraintName("fk_provisioning_jobs_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
