using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Subscriptions
{
    public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
    {
        public void Configure(EntityTypeBuilder<Subscription> builder)
        {
            builder.ToTable("subscriptions", table =>
            {
                table.HasCheckConstraint("ck_subscriptions_status", "status IN ('Active', 'Suspended', 'Expired', 'Cancelled')");
                table.HasCheckConstraint("ck_subscriptions_owner", "num_nonnulls(onboarding_request_id, tenant_id) = 1");
            });

            builder.HasKey(subscription => subscription.Id).HasName("pk_subscriptions");
            builder.Property(subscription => subscription.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(subscription => subscription.OnboardingRequestId).HasColumnName("onboarding_request_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(subscription => subscription.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(subscription => subscription.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(subscription => subscription.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(subscription => subscription.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(subscription => subscription.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(subscription => subscription.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(subscription => subscription.OnboardingRequestId)
                .IsUnique()
                .HasDatabaseName("uq_subscriptions_onboarding_request_id")
                .HasFilter("\"onboarding_request_id\" IS NOT NULL");
            builder.HasIndex(subscription => subscription.TenantId)
                .IsUnique()
                .HasDatabaseName("uq_subscriptions_tenant_id")
                .HasFilter("\"tenant_id\" IS NOT NULL");

            builder.HasOne<OnboardingRequest>()
                .WithMany()
                .HasForeignKey(subscription => subscription.OnboardingRequestId)
                .HasConstraintName("fk_subscriptions_onboarding_request_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
