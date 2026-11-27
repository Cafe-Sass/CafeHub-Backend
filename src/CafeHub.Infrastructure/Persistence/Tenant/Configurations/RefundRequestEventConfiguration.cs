using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class RefundRequestEventConfiguration : IEntityTypeConfiguration<RefundRequestEvent>
    {
        public void Configure(EntityTypeBuilder<RefundRequestEvent> builder)
        {
            builder.ToTable("refund_request_events", table =>
            {
                table.HasCheckConstraint(
                    "ck_refund_request_events_kind",
                    "kind IN ('Submitted', 'Assigned', 'StatusChanged')");
                table.HasCheckConstraint(
                    "ck_refund_request_events_from_status",
                    "from_status IS NULL OR from_status IN ('Open', 'InReview', 'Approved', 'Rejected')");
                table.HasCheckConstraint(
                    "ck_refund_request_events_to_status",
                    "to_status IS NULL OR to_status IN ('Open', 'InReview', 'Approved', 'Rejected')");
                table.HasCheckConstraint(
                    "ck_refund_request_events_actor_kind",
                    "actor_kind IN ('Guest', 'Staff')");
                table.HasCheckConstraint(
                    "ck_refund_request_events_actor_pair",
                    "((actor_kind = 'Guest' AND actor_user_id IS NULL) OR (actor_kind = 'Staff' AND actor_user_id IS NOT NULL))");
            });

            builder.HasKey(entry => entry.Id).HasName("pk_refund_request_events");
            builder.Property(entry => entry.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(entry => entry.RefundRequestId).HasColumnName("refund_request_id").HasColumnType("uuid");
            builder.Property(entry => entry.Kind).HasColumnName("kind").HasColumnType("text").HasConversion<string>();
            builder.Property(entry => entry.FromStatus).HasColumnName("from_status").HasColumnType("text").HasConversion<string>();
            builder.Property(entry => entry.ToStatus).HasColumnName("to_status").HasColumnType("text").HasConversion<string>();
            builder.Property(entry => entry.ActorKind).HasColumnName("actor_kind").HasColumnType("text").HasConversion<string>();
            builder.Property(entry => entry.ActorUserId).HasColumnName("actor_user_id").HasColumnType("uuid");
            builder.Property(entry => entry.Note).HasColumnName("note").HasColumnType("text");
            builder.Property(entry => entry.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamptz");

            builder.HasIndex(entry => entry.RefundRequestId)
                .HasDatabaseName("ix_refund_request_events_refund_request_id");
            builder.HasIndex(entry => entry.ActorUserId)
                .HasDatabaseName("ix_refund_request_events_actor_user_id");
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(entry => entry.ActorUserId)
                .HasConstraintName("fk_refund_request_events_actor_user_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
