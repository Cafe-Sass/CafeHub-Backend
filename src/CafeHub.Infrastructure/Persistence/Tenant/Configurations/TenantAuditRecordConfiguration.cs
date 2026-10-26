using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class TenantAuditRecordConfiguration : IEntityTypeConfiguration<TenantAuditRecord>
    {
        public void Configure(EntityTypeBuilder<TenantAuditRecord> builder)
        {
            builder.ToTable("tenant_audit_records", t =>
            {
                t.HasCheckConstraint("ck_tenant_audit_records_kind", "actor_kind IN ('User', 'System')");
                t.HasCheckConstraint("ck_tenant_audit_records_action_code_length", "length(action_code) >= 1 AND length(action_code) <= 128");
                t.HasCheckConstraint("ck_tenant_audit_records_subject_type_length", "length(subject_type) >= 1 AND length(subject_type) <= 64");
            });

            builder.HasKey(r => r.Id).HasName("pk_tenant_audit_records");
            builder.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(r => r.RecordedAt).HasColumnName("recorded_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(r => r.ActorUserId).HasColumnName("actor_user_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(r => r.ActorKind).HasColumnName("actor_kind").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(r => r.ActionCode).HasColumnName("action_code").HasColumnType("text").IsRequired();
            builder.Property(r => r.SubjectType).HasColumnName("subject_type").HasColumnType("text").IsRequired();
            builder.Property(r => r.SubjectId).HasColumnName("subject_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(r => r.BeforeState).HasColumnName("before_state").HasColumnType("jsonb").IsRequired(false);
            builder.Property(r => r.AfterState).HasColumnName("after_state").HasColumnType("jsonb").IsRequired(false);

            builder.HasIndex(r => r.ActorUserId).HasDatabaseName("ix_tenant_audit_records_actor_user_id");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(r => r.ActorUserId)
                .HasConstraintName("fk_tenant_audit_records_actor_user_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
