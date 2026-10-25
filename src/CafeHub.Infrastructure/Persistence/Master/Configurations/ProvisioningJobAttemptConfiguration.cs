using CafeHub.Domain.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class ProvisioningJobAttemptConfiguration : IEntityTypeConfiguration<global::CafeHub.Domain.Provisioning.ProvisioningJobAttempt>
    {
        public void Configure(EntityTypeBuilder<global::CafeHub.Domain.Provisioning.ProvisioningJobAttempt> builder)
        {
            builder.ToTable("provisioning_job_attempts", table =>
            {
                table.HasCheckConstraint("ck_provisioning_job_attempts_attempt_number_positive", "attempt_number >= 1");
                table.HasCheckConstraint("ck_provisioning_job_attempts_status", "status IN ('Running', 'Succeeded', 'Failed')");
                table.HasCheckConstraint("ck_provisioning_job_attempts_error_code_length", "error_code IS NULL OR length(error_code) <= 128");
            });

            builder.HasKey(attempt => attempt.Id).HasName("pk_provisioning_job_attempts");
            builder.Property(attempt => attempt.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(attempt => attempt.ProvisioningJobId).HasColumnName("provisioning_job_id").HasColumnType("uuid").IsRequired();
            builder.Property(attempt => attempt.AttemptNumber).HasColumnName("attempt_number").HasColumnType("integer").IsRequired();
            builder.Property(attempt => attempt.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(attempt => attempt.StartedAt).HasColumnName("started_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(attempt => attempt.FinishedAt).HasColumnName("finished_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(attempt => attempt.ErrorCode).HasColumnName("error_code").HasColumnType("text").IsRequired(false);
            builder.Property(attempt => attempt.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(attempt => attempt.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

            builder.HasIndex(attempt => new { attempt.ProvisioningJobId, attempt.AttemptNumber })
                .IsUnique()
                .HasDatabaseName("uq_provisioning_job_attempts_attempt");

            builder.HasOne<global::CafeHub.Domain.Provisioning.ProvisioningJob>()
                .WithMany()
                .HasForeignKey(attempt => attempt.ProvisioningJobId)
                .HasConstraintName("fk_provisioning_job_attempts_provisioning_job_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
