using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Subscriptions
{
    public sealed class SubscriptionHistoryConfiguration
        : IEntityTypeConfiguration<SubscriptionHistory>
    {
        public void Configure(EntityTypeBuilder<SubscriptionHistory> builder)
        {
            builder.ToTable("subscription_history", table =>
            {
                table.HasCheckConstraint(
                    "ck_subscription_history_from_status",
                    "from_status IS NULL OR from_status IN ('Active', 'Suspended', 'Expired', 'Cancelled')");
                table.HasCheckConstraint(
                    "ck_subscription_history_to_status",
                    "to_status IN ('Active', 'Suspended', 'Expired', 'Cancelled')");
                table.HasCheckConstraint(
                    "ck_subscription_history_period_number",
                    "period_number IS NULL OR period_number >= 1");
                table.HasCheckConstraint(
                    "ck_subscription_history_actor_reference",
                    "actor_reference IS NULL OR (char_length(actor_reference) BETWEEN 1 AND 256 AND actor_reference !~ '[[:cntrl:]]')");
                table.HasCheckConstraint(
                    "ck_subscription_history_reason",
                    "reason IS NULL OR (char_length(reason) BETWEEN 1 AND 256 AND reason !~ '[[:cntrl:]]')");
            });

            builder.HasKey(history => history.Id).HasName("pk_subscription_history");
            builder.Property(history => history.Id)
                .HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(history => history.SubscriptionId)
                .HasColumnName("subscription_id").HasColumnType("uuid").IsRequired();
            builder.Property(history => history.FromStatus)
                .HasColumnName("from_status").HasColumnType("text").HasConversion<string>();
            builder.Property(history => history.ToStatus)
                .HasColumnName("to_status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(history => history.PlanVersionId)
                .HasColumnName("plan_version_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(history => history.PeriodNumber)
                .HasColumnName("period_number").HasColumnType("integer").IsRequired(false);
            builder.Property(history => history.ActorPlatformAdminId)
                .HasColumnName("actor_platform_admin_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(history => history.ActorReference)
                .HasColumnName("actor_reference").HasColumnType("text").IsRequired(false);
            builder.Property(history => history.Reason)
                .HasColumnName("reason").HasColumnType("text").IsRequired(false);
            builder.Property(history => history.OccurredAt)
                .HasColumnName("occurred_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(history => history.CreatedAt)
                .HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            builder.HasIndex(history => new { history.SubscriptionId, history.OccurredAt })
                .HasDatabaseName("ix_subscription_history_subscription_occurred_at");
            builder.HasIndex(history => history.PlanVersionId)
                .HasDatabaseName("ix_subscription_history_plan_version_id");
            builder.HasIndex(history => history.ActorPlatformAdminId)
                .HasDatabaseName("ix_subscription_history_actor_platform_admin_id");

            builder.HasOne<Subscription>()
                .WithMany()
                .HasForeignKey(history => history.SubscriptionId)
                .HasConstraintName("fk_subscription_history_subscription_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<PlanVersion>()
                .WithMany()
                .HasForeignKey(history => history.PlanVersionId)
                .HasConstraintName("fk_subscription_history_plan_version_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(history => history.ActorPlatformAdminId)
                .HasConstraintName("fk_subscription_history_actor_platform_admin_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
