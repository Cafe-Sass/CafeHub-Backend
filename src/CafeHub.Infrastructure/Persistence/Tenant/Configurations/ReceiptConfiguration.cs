using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
    {
        public void Configure(EntityTypeBuilder<Receipt> builder)
        {
            builder.ToTable("receipts", table =>
            {
                table.HasCheckConstraint(
                    "ck_receipts_receipt_number_format",
                    "char_length(receipt_number) BETWEEN 11 AND 32 AND receipt_number ~ '^R-[0-9]{8}-[0-9]+$'");
                table.HasCheckConstraint("ck_receipts_total_amount_range", "total_amount >= 0");
                table.HasCheckConstraint("ck_receipts_paid_amount_range", "paid_amount > 0");
                table.HasCheckConstraint("ck_receipts_fully_paid", "remaining_amount = 0");
            });

            builder.HasKey(receipt => receipt.Id).HasName("pk_receipts");
            builder.Property(receipt => receipt.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(receipt => receipt.BillId).HasColumnName("bill_id").HasColumnType("uuid");
            builder.Property(receipt => receipt.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(receipt => receipt.ReceiptNumber).HasColumnName("receipt_number").HasColumnType("text");
            builder.Property(receipt => receipt.IssuedAt).HasColumnName("issued_at").HasColumnType("timestamptz");
            builder.Property(receipt => receipt.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(receipt => receipt.PaidAmount).HasColumnName("paid_amount").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(receipt => receipt.RemainingAmount).HasColumnName("remaining_amount").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(receipt => receipt.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(receipt => receipt.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");

            builder.HasIndex(receipt => receipt.BillId)
                .HasDatabaseName("uq_receipts_bill_id")
                .IsUnique();
            builder.HasIndex(receipt => new { receipt.BranchId, receipt.ReceiptNumber })
                .HasDatabaseName("uq_receipts_branch_receipt_number")
                .IsUnique();
            builder.HasIndex(receipt => new { receipt.BranchId, receipt.IssuedAt })
                .HasDatabaseName("ix_receipts_branch_issued_at");
            builder.HasIndex(receipt => receipt.CreatedBy).HasDatabaseName("ix_receipts_created_by");

            builder.HasOne<Bill>()
                .WithMany()
                .HasForeignKey(receipt => receipt.BillId)
                .HasConstraintName("fk_receipts_bill_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(receipt => receipt.BranchId)
                .HasConstraintName("fk_receipts_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(receipt => receipt.CreatedBy)
                .HasConstraintName("fk_receipts_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(receipt => receipt.Lines)
                .WithOne()
                .HasForeignKey(line => line.ReceiptId)
                .HasConstraintName("fk_receipt_lines_receipt_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(receipt => receipt.Payments)
                .WithOne()
                .HasForeignKey(payment => payment.ReceiptId)
                .HasConstraintName("fk_receipt_payments_receipt_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
