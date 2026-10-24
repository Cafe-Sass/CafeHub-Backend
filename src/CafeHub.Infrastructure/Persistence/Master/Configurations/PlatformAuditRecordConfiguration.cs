using CafeHub.Infrastructure.Persistence.Master.Audit;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class PlatformAuditRecordConfiguration : IEntityTypeConfiguration<PlatformAuditRecord>
    {
        public void Configure(EntityTypeBuilder<PlatformAuditRecord> builder)
        {
            builder.ToTable("platform_audit_records", t =>
            {
                t.HasCheckConstraint("ck_platform_audit_records_kind", "actor_kind IN ('PlatformAdmin', 'System')");
                t.HasCheckConstraint("ck_platform_audit_records_action_code_length", "length(action_code) >= 1 AND length(action_code) <= 128");
                t.HasCheckConstraint("ck_platform_audit_records_subject_type_length", "length(subject_type) >= 1 AND length(subject_type) <= 64");
                t.HasCheckConstraint("ck_platform_audit_records_correlation_id_format", "correlation_id IS NULL OR correlation_id ~ '^[A-Za-z0-9._:-]{1,64}$'");
                t.HasCheckConstraint("ck_platform_audit_records_reason_code_format", "reason_code IS NULL OR reason_code ~ '^[a-z][a-z0-9_]{0,63}$'");
            });

            builder.HasKey(r => r.Id).HasName("pk_platform_audit_records");
            builder.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(r => r.RecordedAt).HasColumnName("recorded_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(r => r.ActorPlatformAdminId).HasColumnName("actor_platform_admin_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(r => r.ActorKind).HasColumnName("actor_kind").HasColumnType("text").IsRequired();
            builder.Property(r => r.ActionCode).HasColumnName("action_code").HasColumnType("text").IsRequired();
            builder.Property(r => r.SubjectType).HasColumnName("subject_type").HasColumnType("text").IsRequired();
            builder.Property(r => r.SubjectId).HasColumnName("subject_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(r => r.CorrelationId).HasColumnName("correlation_id").HasColumnType("text").IsRequired(false);
            builder.Property(r => r.ReasonCode).HasColumnName("reason_code").HasColumnType("text").IsRequired(false);
            builder.Property(r => r.BeforeState).HasColumnName("before_state").HasColumnType("jsonb").IsRequired(false);
            builder.Property(r => r.AfterState).HasColumnName("after_state").HasColumnType("jsonb").IsRequired(false);

            builder.HasIndex(r => r.ActorPlatformAdminId).HasDatabaseName("ix_platform_audit_records_actor_platform_admin_id");

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(r => r.ActorPlatformAdminId)
                .HasConstraintName("fk_platform_audit_records_actor_platform_admin_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
