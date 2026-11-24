using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("orders", table =>
            {
                table.HasCheckConstraint("ck_orders_order_number", "order_number >= 1");
                table.HasCheckConstraint("ck_orders_service_type", "service_type IN ('DineIn', 'Takeaway', 'WifiOnly')");
                table.HasCheckConstraint(
                    "ck_orders_service_table",
                    "(service_type = 'WifiOnly' AND table_id IS NULL) OR (service_type <> 'WifiOnly' AND table_id IS NOT NULL)");
                table.HasCheckConstraint("ck_orders_status", "status IN ('Pending', 'Accepted', 'Rejected', 'Expired')");
                table.HasCheckConstraint("ck_orders_source", "source IN ('Guest', 'Captain')");
                table.HasCheckConstraint(
                    "ck_orders_guest_display_name_length", "char_length(guest_display_name) BETWEEN 1 AND 64");
                table.HasCheckConstraint(
                    "ck_orders_guest_device_id_format",
                    "guest_device_id IS NULL OR (char_length(guest_device_id) BETWEEN 16 AND 64 AND guest_device_id ~ '^[A-Za-z0-9_-]+$')");
                table.HasCheckConstraint(
                    "ck_orders_public_reference_format",
                    "char_length(public_reference) = 22 AND public_reference ~ '^[A-Za-z0-9_-]{22}$'");
                table.HasCheckConstraint(
                    "ck_orders_idempotency_key_length", "char_length(idempotency_key) BETWEEN 1 AND 128");
                table.HasCheckConstraint(
                    "ck_orders_request_hash_format", "request_hash IS NULL OR request_hash ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_orders_total_amount_range", "total_amount >= 0");
                table.HasCheckConstraint("ck_orders_response_deadline", "response_deadline > submitted_at");
                table.HasCheckConstraint(
                    "ck_orders_decision_pair",
                    "(responded_at IS NULL AND responded_by_user_id IS NULL AND rejection_reason IS NULL) OR (responded_at IS NOT NULL AND responded_by_user_id IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_orders_rejection_reason",
                    "rejection_reason IS NULL OR (status = 'Rejected' AND char_length(rejection_reason) BETWEEN 1 AND 256)");
            });

            builder.HasKey(order => order.Id).HasName("pk_orders");
            builder.Property(order => order.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(order => order.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(order => order.TableId).HasColumnName("table_id").HasColumnType("uuid");
            builder.Property(order => order.OrderNumber).HasColumnName("order_number").HasColumnType("integer");
            builder.Property(order => order.BusinessDate).HasColumnName("business_date").HasColumnType("date");
            builder.Property(order => order.ServiceType).HasColumnName("service_type").HasColumnType("text").HasConversion<string>();
            builder.Property(order => order.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(order => order.Source).HasColumnName("source").HasColumnType("text").HasConversion<string>();
            builder.Property(order => order.GuestDisplayName).HasColumnName("guest_display_name").HasColumnType("text");
            builder.Property(order => order.GuestDeviceId).HasColumnName("guest_device_id").HasColumnType("text");
            builder.Property(order => order.PlacedByUserId).HasColumnName("placed_by_user_id").HasColumnType("uuid");
            builder.Property(order => order.PublicReference).HasColumnName("public_reference").HasColumnType("text");
            builder.Property(order => order.IdempotencyKey).HasColumnName("idempotency_key").HasColumnType("text");
            builder.Property(order => order.RequestHash).HasColumnName("request_hash").HasColumnType("text");
            builder.Property(order => order.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(order => order.SubmittedAt).HasColumnName("submitted_at").HasColumnType("timestamptz");
            builder.Property(order => order.ResponseDeadline).HasColumnName("response_deadline").HasColumnType("timestamptz");
            builder.Property(order => order.RespondedByUserId).HasColumnName("responded_by_user_id").HasColumnType("uuid");
            builder.Property(order => order.RespondedAt).HasColumnName("responded_at").HasColumnType("timestamptz");
            builder.Property(order => order.RejectionReason).HasColumnName("rejection_reason").HasColumnType("text");
            builder.Property(order => order.BillId).HasColumnName("bill_id").HasColumnType("uuid");
            builder.Property(order => order.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(order => order.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(order => order.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(order => order.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(order => new { order.BranchId, order.BusinessDate, order.OrderNumber })
                .HasDatabaseName("uq_orders_branch_business_date_number")
                .IsUnique();
            builder.HasIndex(order => order.PublicReference)
                .HasDatabaseName("uq_orders_public_reference")
                .IsUnique();
            builder.HasIndex(order => new { order.BranchId, order.IdempotencyKey })
                .HasDatabaseName("uq_orders_branch_idempotency_key")
                .IsUnique();
            builder.HasIndex(order => new { order.BranchId, order.Status, order.SubmittedAt })
                .HasDatabaseName("ix_orders_branch_id_status_submitted_at");
            builder.HasIndex(order => order.TableId).HasDatabaseName("ix_orders_table_id");
            builder.HasIndex(order => order.PlacedByUserId).HasDatabaseName("ix_orders_placed_by_user_id");
            builder.HasIndex(order => order.RespondedByUserId).HasDatabaseName("ix_orders_responded_by_user_id");
            builder.HasIndex(order => order.CreatedBy).HasDatabaseName("ix_orders_created_by");
            builder.HasIndex(order => order.UpdatedBy).HasDatabaseName("ix_orders_updated_by");
            builder.HasIndex(order => order.BillId).HasDatabaseName("ix_orders_bill_id");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(order => order.BranchId)
                .HasConstraintName("fk_orders_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<BranchTable>()
                .WithMany()
                .HasForeignKey(order => order.TableId)
                .HasConstraintName("fk_orders_table_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(order => order.PlacedByUserId)
                .HasConstraintName("fk_orders_placed_by_user_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(order => order.RespondedByUserId)
                .HasConstraintName("fk_orders_responded_by_user_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(order => order.CreatedBy)
                .HasConstraintName("fk_orders_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(order => order.UpdatedBy)
                .HasConstraintName("fk_orders_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Bill>()
                .WithMany()
                .HasForeignKey(order => order.BillId)
                .HasConstraintName("fk_orders_bill_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(order => order.Items)
                .WithOne()
                .HasForeignKey(item => item.OrderId)
                .HasConstraintName("fk_order_items_order_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
