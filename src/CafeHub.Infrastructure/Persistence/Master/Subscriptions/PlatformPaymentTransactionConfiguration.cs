using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Subscriptions
{
    public sealed class PlatformPaymentTransactionConfiguration : IEntityTypeConfiguration<PlatformPaymentTransaction>
    {
        public void Configure(EntityTypeBuilder<PlatformPaymentTransaction> builder)
        {
            builder.ToTable("platform_payment_transactions", table =>
            {
                table.HasCheckConstraint("ck_platform_payment_transactions_target", "num_nonnulls(onboarding_request_id, renewal_request_id) = 1");
                table.HasCheckConstraint("ck_platform_payment_transactions_status", "status IN ('Initiated', 'Succeeded', 'Failed')");
                table.HasCheckConstraint("ck_platform_payment_transactions_idempotency_key_length", "length(idempotency_key) >= 8 AND length(idempotency_key) <= 128");
                table.HasCheckConstraint("ck_platform_payment_transactions_amount_non_negative", "amount >= 0");
                table.HasCheckConstraint("ck_platform_payment_transactions_currency_code_format", "currency_code ~ '^[a-z]{3}$'");
                table.HasCheckConstraint("ck_platform_payment_transactions_failure_code_length", "failure_code IS NULL OR length(failure_code) <= 128");
            });

            builder.HasKey(transaction => transaction.Id).HasName("pk_platform_payment_transactions");
            builder.Property(transaction => transaction.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(transaction => transaction.OnboardingRequestId).HasColumnName("onboarding_request_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(transaction => transaction.RenewalRequestId).HasColumnName("renewal_request_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(transaction => transaction.IdempotencyKey).HasColumnName("idempotency_key").HasColumnType("text").IsRequired();
            builder.Property(transaction => transaction.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").IsRequired();
            builder.Property(transaction => transaction.CurrencyCode).HasColumnName("currency_code").HasColumnType("text").IsRequired();
            builder.Property(transaction => transaction.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(transaction => transaction.SettledAt).HasColumnName("settled_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(transaction => transaction.FailureCode).HasColumnName("failure_code").HasColumnType("text").IsRequired(false);
            builder.Property(transaction => transaction.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(transaction => transaction.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(transaction => transaction.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(transaction => transaction.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(transaction => transaction.IdempotencyKey)
                .IsUnique()
                .HasDatabaseName("uq_platform_payment_transactions_key");
            builder.HasIndex(transaction => transaction.OnboardingRequestId).HasDatabaseName("ix_platform_payment_transactions_onboarding_request_id");
            builder.HasIndex(transaction => transaction.RenewalRequestId).HasDatabaseName("ix_platform_payment_transactions_renewal_request_id");

            builder.HasOne<OnboardingRequest>()
                .WithMany()
                .HasForeignKey(transaction => transaction.OnboardingRequestId)
                .HasConstraintName("fk_platform_payment_transactions_onboarding_request_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
