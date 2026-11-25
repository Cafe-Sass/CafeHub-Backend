using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class InventoryBatchConfiguration : IEntityTypeConfiguration<InventoryBatch>
    {
        public void Configure(EntityTypeBuilder<InventoryBatch> builder)
        {
            builder.ToTable("inventory_batches", table =>
            {
                table.HasCheckConstraint(
                    "ck_inventory_batches_received_quantity", "received_quantity >= 0");
                table.HasCheckConstraint(
                    "ck_inventory_batches_remaining_quantity",
                    "remaining_quantity >= 0 AND remaining_quantity <= received_quantity");
                table.HasCheckConstraint(
                    "ck_inventory_batches_expiry_not_before_received",
                    "expiry_date IS NULL OR expiry_date >= received_date");
                table.HasCheckConstraint(
                    "ck_inventory_batches_idempotency_key_length",
                    "char_length(idempotency_key) BETWEEN 8 AND 64");
            });

            builder.HasKey(batch => batch.Id).HasName("pk_inventory_batches");
            builder.Property(batch => batch.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(batch => batch.ItemId).HasColumnName("item_id").HasColumnType("uuid");
            builder.Property(batch => batch.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(batch => batch.ReceivedQuantity)
                .HasColumnName("received_quantity").HasPrecision(12, 3);
            builder.Property(batch => batch.RemainingQuantity)
                .HasColumnName("remaining_quantity").HasPrecision(12, 3);
            builder.Property(batch => batch.ReceivedDate).HasColumnName("received_date").HasColumnType("date");
            builder.Property(batch => batch.ExpiryDate).HasColumnName("expiry_date").HasColumnType("date");
            builder.Property(batch => batch.ReceivedByUserId)
                .HasColumnName("received_by_user_id").HasColumnType("uuid");
            builder.Property(batch => batch.IdempotencyKey)
                .HasColumnName("idempotency_key").HasColumnType("text");
            builder.Property(batch => batch.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(batch => batch.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(batch => batch.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(batch => batch.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(batch => new { batch.BranchId, batch.IdempotencyKey })
                .HasDatabaseName("uq_inventory_batches_branch_idempotency_key")
                .IsUnique();
            builder.HasIndex(batch => batch.ItemId).HasDatabaseName("ix_inventory_batches_item_id");
            builder.HasIndex(batch => batch.ReceivedByUserId)
                .HasDatabaseName("ix_inventory_batches_received_by_user_id");
            builder.HasIndex(batch => batch.CreatedBy).HasDatabaseName("ix_inventory_batches_created_by");
            builder.HasIndex(batch => batch.UpdatedBy).HasDatabaseName("ix_inventory_batches_updated_by");

            builder.HasOne<InventoryItem>()
                .WithMany()
                .HasForeignKey(batch => batch.ItemId)
                .HasConstraintName("fk_inventory_batches_item_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(batch => batch.BranchId)
                .HasConstraintName("fk_inventory_batches_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(batch => batch.ReceivedByUserId)
                .HasConstraintName("fk_inventory_batches_received_by_user_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(batch => batch.CreatedBy)
                .HasConstraintName("fk_inventory_batches_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(batch => batch.UpdatedBy)
                .HasConstraintName("fk_inventory_batches_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
