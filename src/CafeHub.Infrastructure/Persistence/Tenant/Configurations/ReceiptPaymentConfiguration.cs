using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class ReceiptPaymentConfiguration : IEntityTypeConfiguration<ReceiptPayment>
    {
        public void Configure(EntityTypeBuilder<ReceiptPayment> builder)
        {
            builder.ToTable("receipt_payments", table =>
            {
                table.HasCheckConstraint("ck_receipt_payments_method", "method IN ('Cash', 'Online')");
                table.HasCheckConstraint("ck_receipt_payments_amount_range", "amount > 0");
            });

            builder.HasKey(payment => payment.Id).HasName("pk_receipt_payments");
            builder.Property(payment => payment.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(payment => payment.ReceiptId).HasColumnName("receipt_id").HasColumnType("uuid");
            builder.Property(payment => payment.Method).HasColumnName("method").HasColumnType("text").HasConversion<string>();
            builder.Property(payment => payment.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(payment => payment.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamptz");
            builder.Property(payment => payment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            builder.HasIndex(payment => payment.ReceiptId)
                .HasDatabaseName("ix_receipt_payments_receipt_id");
        }
    }
}
