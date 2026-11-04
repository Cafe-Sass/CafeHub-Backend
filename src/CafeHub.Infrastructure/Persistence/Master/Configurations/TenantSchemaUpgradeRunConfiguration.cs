using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class TenantSchemaUpgradeRunConfiguration : IEntityTypeConfiguration<global::CafeHub.Domain.Tenants.TenantSchemaUpgradeRun>
    {
        public void Configure(EntityTypeBuilder<global::CafeHub.Domain.Tenants.TenantSchemaUpgradeRun> builder)
        {
            builder.ToTable("tenant_schema_upgrade_runs", table =>
            {
                table.HasCheckConstraint("ck_tenant_schema_upgrade_runs_status", "status IN ('Pending', 'Running', 'Succeeded', 'Failed')");
                table.HasCheckConstraint("ck_tenant_schema_upgrade_runs_target_migration_id_length", "length(target_migration_id) >= 1 AND length(target_migration_id) <= 150");
                table.HasCheckConstraint("ck_tenant_schema_upgrade_runs_failure_code_length", "failure_code IS NULL OR (length(failure_code) >= 1 AND length(failure_code) <= 64)");
                table.HasCheckConstraint("ck_tenant_schema_upgrade_runs_attempt_count_not_negative", "attempt_count >= 0");
            });

            builder.HasKey(run => run.Id).HasName("pk_tenant_schema_upgrade_runs");
            builder.Property(run => run.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(run => run.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
            builder.Property(run => run.TargetMigrationId).HasColumnName("target_migration_id").HasColumnType("text").IsRequired();
            builder.Property(run => run.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(run => run.FailureCode).HasColumnName("failure_code").HasColumnType("text").IsRequired(false);
            builder.Property(run => run.AttemptCount).HasColumnName("attempt_count").HasColumnType("integer").IsRequired();
            builder.Property(run => run.RequestedBy).HasColumnName("requested_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(run => run.StartedAt).HasColumnName("started_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(run => run.FinishedAt).HasColumnName("finished_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(run => run.LeaseExpiresAt).HasColumnName("lease_expires_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(run => run.LeasedBy).HasColumnName("leased_by").HasColumnType("text").IsRequired(false);
            builder.Property(run => run.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(run => run.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(run => run.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(run => run.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(run => run.TenantId, "ix_tenant_schema_upgrade_runs_tenant_id");

            builder.HasIndex(run => run.TenantId, "uq_tenant_schema_upgrade_runs_active")
                .IsUnique()
                .HasFilter("status IN ('Pending', 'Running')");

            builder.HasOne<global::CafeHub.Domain.Tenants.Tenant>()
                .WithMany()
                .HasForeignKey(run => run.TenantId)
                .HasConstraintName("fk_tenant_schema_upgrade_runs_tenant_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(run => run.RequestedBy)
                .HasConstraintName("fk_tenant_schema_upgrade_runs_requested_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(run => run.CreatedBy)
                .HasConstraintName("fk_tenant_schema_upgrade_runs_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(run => run.UpdatedBy)
                .HasConstraintName("fk_tenant_schema_upgrade_runs_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
