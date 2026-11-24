using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("payments", table =>
            {
                table.HasCheckConstraint("ck_payments_method", "method IN ('Cash', 'Online')");
                table.HasCheckConstraint(
                    "ck_payments_status", "status IN ('Confirmed', 'PendingReview', 'Rejected')");
                table.HasCheckConstraint("ck_payments_amount_range", "amount > 0");
                table.HasCheckConstraint(
                    "ck_payments_confirmation_pair",
                    "(status = 'Confirmed') = (confirmed_at IS NOT NULL AND confirmed_by_user_id IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_payments_idempotency_key_length", "char_length(idempotency_key) BETWEEN 1 AND 128");
                table.HasCheckConstraint(
                    "ck_payments_media_reference_length", "media_reference IS NULL OR length(media_reference) <= 512");
                table.HasCheckConstraint(
                    "ck_payments_claimed_amount_range", "claimed_amount IS NULL OR claimed_amount > 0");
                table.HasCheckConstraint(
                    "ck_payments_note_length",
                    "note IS NULL OR (length(note) >= 1 AND length(note) <= 140 AND note !~ '[[:cntrl:]]')");
                table.HasCheckConstraint(
                    "ck_payments_online_proof",
                    "method <> 'Online' OR (media_reference IS NOT NULL AND claimed_amount IS NOT NULL)");
            });

            builder.HasKey(payment => payment.Id).HasName("pk_payments");
            builder.Property(payment => payment.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(payment => payment.BillId).HasColumnName("bill_id").HasColumnType("uuid");
            builder.Property(payment => payment.OrderId).HasColumnName("order_id").HasColumnType("uuid");
            builder.Property(payment => payment.Method).HasColumnName("method").HasColumnType("text").HasConversion<string>();
            builder.Property(payment => payment.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(payment => payment.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(payment => payment.ConfirmedByUserId).HasColumnName("confirmed_by_user_id").HasColumnType("uuid");
            builder.Property(payment => payment.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamptz");
            builder.Property(payment => payment.IdempotencyKey).HasColumnName("idempotency_key").HasColumnType("text");
            builder.Property(payment => payment.MediaReference).HasColumnName("media_reference").HasColumnType("text");
            builder.Property(payment => payment.ClaimedAmount).HasColumnName("claimed_amount").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(payment => payment.Note).HasColumnName("note").HasColumnType("text");
            builder.Property(payment => payment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(payment => payment.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(payment => payment.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(payment => payment.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(payment => new { payment.BillId, payment.IdempotencyKey })
                .HasDatabaseName("uq_payments_bill_idempotency_key")
                .IsUnique();
            builder.HasIndex(payment => payment.OrderId).HasDatabaseName("ix_payments_order_id");
            builder.HasIndex(payment => payment.ConfirmedByUserId).HasDatabaseName("ix_payments_confirmed_by_user_id");
            builder.HasIndex(payment => payment.CreatedBy).HasDatabaseName("ix_payments_created_by");
            builder.HasIndex(payment => payment.UpdatedBy).HasDatabaseName("ix_payments_updated_by");

            builder.HasOne<Bill>()
                .WithMany()
                .HasForeignKey(payment => payment.BillId)
                .HasConstraintName("fk_payments_bill_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(payment => payment.OrderId)
                .HasConstraintName("fk_payments_order_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(payment => payment.ConfirmedByUserId)
                .HasConstraintName("fk_payments_confirmed_by_user_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(payment => payment.CreatedBy)
                .HasConstraintName("fk_payments_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(payment => payment.UpdatedBy)
                .HasConstraintName("fk_payments_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
