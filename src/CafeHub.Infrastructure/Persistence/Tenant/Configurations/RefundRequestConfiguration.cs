using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
    {
        public void Configure(EntityTypeBuilder<RefundRequest> builder)
        {
            builder.ToTable("refund_requests", table =>
            {
                table.HasCheckConstraint(
                    "ck_refund_requests_public_reference_format",
                    "char_length(public_reference) = 22 AND public_reference ~ '^[A-Za-z0-9_-]{22}$'");
                table.HasCheckConstraint(
                    "ck_refund_requests_idempotency_key_format",
                    "char_length(idempotency_key) BETWEEN 8 AND 64 AND idempotency_key ~ '^[A-Za-z0-9_-]+$'");
                table.HasCheckConstraint(
                    "ck_refund_requests_status",
                    "status IN ('Open', 'InReview', 'Approved', 'Rejected')");
                table.HasCheckConstraint(
                    "ck_refund_requests_reason_length",
                    "char_length(btrim(reason)) BETWEEN 3 AND 256");
                table.HasCheckConstraint(
                    "ck_refund_requests_details_length",
                    "details IS NULL OR char_length(details) <= 2000");
                table.HasCheckConstraint("ck_refund_requests_claimed_amount", "claimed_amount > 0");
                table.HasCheckConstraint(
                    "ck_refund_requests_decision_pair",
                    "((status IN ('Approved', 'Rejected') AND resolution_note IS NOT NULL AND char_length(btrim(resolution_note)) > 0 AND decided_by_user_id IS NOT NULL AND decided_at IS NOT NULL) OR (status IN ('Open', 'InReview') AND resolution_note IS NULL AND decided_by_user_id IS NULL AND decided_at IS NULL))");
            });

            builder.HasKey(request => request.Id).HasName("pk_refund_requests");
            builder.Property(request => request.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(request => request.BillId).HasColumnName("bill_id").HasColumnType("uuid");
            builder.Property(request => request.PaymentId).HasColumnName("payment_id").HasColumnType("uuid");
            builder.Property(request => request.OrderId).HasColumnName("order_id").HasColumnType("uuid");
            builder.Property(request => request.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(request => request.PublicReference).HasColumnName("public_reference").HasColumnType("text");
            builder.Property(request => request.IdempotencyKey).HasColumnName("idempotency_key").HasColumnType("text");
            builder.Property(request => request.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(request => request.Reason).HasColumnName("reason").HasColumnType("text");
            builder.Property(request => request.Details).HasColumnName("details").HasColumnType("text");
            builder.Property(request => request.ClaimedAmount)
                .HasColumnName("claimed_amount").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(request => request.GuestDisplayName).HasColumnName("guest_display_name").HasColumnType("text");
            builder.Property(request => request.AssignedUserId).HasColumnName("assigned_user_id").HasColumnType("uuid");
            builder.Property(request => request.ResolutionNote).HasColumnName("resolution_note").HasColumnType("text");
            builder.Property(request => request.DecidedByUserId).HasColumnName("decided_by_user_id").HasColumnType("uuid");
            builder.Property(request => request.DecidedAt).HasColumnName("decided_at").HasColumnType("timestamptz");
            builder.Property(request => request.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(request => request.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(request => request.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(request => request.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property<uint>("xmin").IsRowVersion();

            builder.HasIndex(request => request.PublicReference)
                .HasDatabaseName("uq_refund_requests_public_reference")
                .IsUnique();
            builder.HasIndex(request => new { request.BranchId, request.IdempotencyKey })
                .HasDatabaseName("uq_refund_requests_branch_idempotency_key")
                .IsUnique();
            builder.HasIndex(request => request.BillId).HasDatabaseName("ix_refund_requests_bill_id");
            builder.HasIndex(request => request.PaymentId).HasDatabaseName("ix_refund_requests_payment_id");
            builder.HasIndex(request => request.OrderId).HasDatabaseName("ix_refund_requests_order_id");
            builder.HasIndex(request => request.BranchId).HasDatabaseName("ix_refund_requests_branch_id");
            builder.HasIndex(request => request.AssignedUserId).HasDatabaseName("ix_refund_requests_assigned_user_id");
            builder.HasIndex(request => request.DecidedByUserId).HasDatabaseName("ix_refund_requests_decided_by_user_id");
            builder.HasIndex(request => request.CreatedBy).HasDatabaseName("ix_refund_requests_created_by");
            builder.HasIndex(request => request.UpdatedBy).HasDatabaseName("ix_refund_requests_updated_by");

            builder.HasOne<Bill>()
                .WithMany()
                .HasForeignKey(request => request.BillId)
                .HasConstraintName("fk_refund_requests_bill_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Payment>()
                .WithMany()
                .HasForeignKey(request => request.PaymentId)
                .HasConstraintName("fk_refund_requests_payment_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(request => request.OrderId)
                .HasConstraintName("fk_refund_requests_order_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(request => request.BranchId)
                .HasConstraintName("fk_refund_requests_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(request => request.AssignedUserId)
                .HasConstraintName("fk_refund_requests_assigned_user_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(request => request.DecidedByUserId)
                .HasConstraintName("fk_refund_requests_decided_by_user_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(request => request.CreatedBy)
                .HasConstraintName("fk_refund_requests_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(request => request.UpdatedBy)
                .HasConstraintName("fk_refund_requests_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(request => request.Attachments)
                .WithOne()
                .HasForeignKey(attachment => attachment.RefundRequestId)
                .HasConstraintName("fk_refund_request_attachments_refund_request_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(request => request.Events)
                .WithOne()
                .HasForeignKey(entry => entry.RefundRequestId)
                .HasConstraintName("fk_refund_request_events_refund_request_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.Navigation(request => request.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(request => request.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
