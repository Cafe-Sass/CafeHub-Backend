using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Subscriptions
{
    public sealed class SubscriptionPeriodConfiguration : IEntityTypeConfiguration<SubscriptionPeriod>
    {
        public void Configure(EntityTypeBuilder<SubscriptionPeriod> builder)
        {
            builder.ToTable("subscription_periods", table =>
            {
                table.HasCheckConstraint("ck_subscription_periods_period_number_positive", "period_number >= 1");
                table.HasCheckConstraint("ck_subscription_periods_status", "period_status IN ('Active', 'Ended', 'Cancelled')");
                table.HasCheckConstraint("ck_subscription_periods_price_non_negative", "agreed_price >= 0");
                table.HasCheckConstraint("ck_subscription_periods_currency_code_format", "currency_code ~ '^[a-z]{3}$'");
            });

            builder.HasKey(period => period.Id).HasName("pk_subscription_periods");
            builder.Property(period => period.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(period => period.SubscriptionId).HasColumnName("subscription_id").HasColumnType("uuid").IsRequired();
            builder.Property(period => period.PlanVersionId).HasColumnName("plan_version_id").HasColumnType("uuid").IsRequired();
            builder.Property(period => period.PeriodNumber).HasColumnName("period_number").HasColumnType("integer").IsRequired();
            builder.Property(period => period.PeriodStatus).HasColumnName("period_status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(period => period.AgreedPrice).HasColumnName("agreed_price").HasColumnType("numeric(12,2)").IsRequired();
            builder.Property(period => period.CurrencyCode).HasColumnName("currency_code").HasColumnType("text").IsRequired();
            builder.Property(period => period.StartsAt).HasColumnName("starts_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(period => period.EndsAt).HasColumnName("ends_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(period => period.RenewalRequestId).HasColumnName("renewal_request_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(period => period.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(period => period.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(period => period.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(period => period.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(period => new { period.SubscriptionId, period.PeriodNumber })
                .IsUnique()
                .HasDatabaseName("uq_subscription_periods_period_number");
            builder.HasIndex(period => period.SubscriptionId)
                .IsUnique()
                .HasDatabaseName("uq_subscription_periods_active")
                .HasFilter("\"period_status\" = 'Active'");
            builder.HasIndex(period => period.RenewalRequestId)
                .IsUnique()
                .HasDatabaseName("uq_subscription_periods_renewal_request")
                .HasFilter("\"renewal_request_id\" IS NOT NULL");

            builder.HasOne<Subscription>()
                .WithMany()
                .HasForeignKey(period => period.SubscriptionId)
                .HasConstraintName("fk_subscription_periods_subscription_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlanVersion>()
                .WithMany()
                .HasForeignKey(period => period.PlanVersionId)
                .HasConstraintName("fk_subscription_periods_plan_version_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
