using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Subscriptions
{
    public sealed class SubscriptionRenewalRequestConfiguration
        : IEntityTypeConfiguration<SubscriptionRenewalRequest>
    {
        public void Configure(EntityTypeBuilder<SubscriptionRenewalRequest> builder)
        {
            builder.ToTable("subscription_renewal_requests", table =>
            {
                table.HasCheckConstraint(
                    "ck_subscription_renewal_requests_status",
                    "status IN ('Pending', 'Approved', 'Declined', 'Expired', 'Completed')");
                table.HasCheckConstraint(
                    "ck_subscription_renewal_requests_price_non_negative",
                    "proposed_price >= 0");
                table.HasCheckConstraint(
                    "ck_subscription_renewal_requests_currency_code_format",
                    "proposed_currency_code ~ '^[a-z]{3}$'");
                table.HasCheckConstraint(
                    "ck_subscription_renewal_requests_expiry",
                    "expires_at > requested_at");
                table.HasCheckConstraint(
                    "ck_subscription_renewal_requests_decided_by",
                    "decided_by IS NULL OR char_length(decided_by) BETWEEN 1 AND 256");
                table.HasCheckConstraint(
                    "ck_subscription_renewal_requests_decided",
                    "(status = 'Pending') = (decided_at IS NULL)");
            });

            builder.HasKey(request => request.Id).HasName("pk_subscription_renewal_requests");
            builder.Property(request => request.Id)
                .HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(request => request.SubscriptionId)
                .HasColumnName("subscription_id").HasColumnType("uuid").IsRequired();
            builder.Property(request => request.PlanVersionId)
                .HasColumnName("plan_version_id").HasColumnType("uuid").IsRequired();
            builder.Property(request => request.ProposedPrice)
                .HasColumnName("proposed_price").HasColumnType("numeric(12,2)").IsRequired();
            builder.Property(request => request.ProposedCurrencyCode)
                .HasColumnName("proposed_currency_code").HasColumnType("text").IsRequired();
            builder.Property(request => request.Status)
                .HasColumnName("status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(request => request.RequestedAt)
                .HasColumnName("requested_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(request => request.ExpiresAt)
                .HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(request => request.DecidedAt)
                .HasColumnName("decided_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(request => request.DecidedBy)
                .HasColumnName("decided_by").HasColumnType("text").IsRequired(false);
            builder.Property(request => request.CreatedAt)
                .HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(request => request.UpdatedAt)
                .HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(request => request.CreatedBy)
                .HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(request => request.UpdatedBy)
                .HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(request => request.SubscriptionId)
                .IsUnique()
                .HasDatabaseName("uq_subscription_renewal_requests_open")
                .HasFilter("\"status\" IN ('Pending', 'Approved')");
            builder.HasIndex(request => new { request.SubscriptionId, request.RequestedAt })
                .HasDatabaseName("ix_subscription_renewal_requests_subscription_requested_at");
            builder.HasIndex(request => request.PlanVersionId)
                .HasDatabaseName("ix_subscription_renewal_requests_plan_version_id");

            builder.HasOne<Subscription>()
                .WithMany()
                .HasForeignKey(request => request.SubscriptionId)
                .HasConstraintName("fk_subscription_renewal_requests_subscription_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<PlanVersion>()
                .WithMany()
                .HasForeignKey(request => request.PlanVersionId)
                .HasConstraintName("fk_subscription_renewal_requests_plan_version_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
