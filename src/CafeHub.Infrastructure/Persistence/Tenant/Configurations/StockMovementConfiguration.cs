using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
    {
        public void Configure(EntityTypeBuilder<StockMovement> builder)
        {
            builder.ToTable("stock_movements", table =>
            {
                table.HasCheckConstraint(
                    "ck_stock_movements_type",
                    "type IN ('Receive', 'Consume', 'Waste', 'Adjust')");
                table.HasCheckConstraint(
                    "ck_stock_movements_quantity",
                    "quantity <> 0 AND quantity >= -999999999.999 AND quantity <= 999999999.999");
                table.HasCheckConstraint(
                    "ck_stock_movements_quantity_sign",
                    "((type = 'Receive' AND quantity > 0) OR (type IN ('Consume', 'Waste') AND quantity < 0) OR (type = 'Adjust' AND quantity <> 0))");
                table.HasCheckConstraint(
                    "ck_stock_movements_reason",
                    "reason IS NULL OR (char_length(btrim(reason)) BETWEEN 1 AND 500 AND reason !~ '[[:cntrl:]]')");
                table.HasCheckConstraint(
                    "ck_stock_movements_reason_required",
                    "type = 'Receive' OR reason IS NOT NULL");
                table.HasCheckConstraint(
                    "ck_stock_movements_idempotency_key_length",
                    "char_length(idempotency_key) BETWEEN 8 AND 64");
            });

            builder.HasKey(movement => movement.Id).HasName("pk_stock_movements");
            builder.Property(movement => movement.Id)
                .HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(movement => movement.ItemId).HasColumnName("item_id").HasColumnType("uuid");
            builder.Property(movement => movement.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(movement => movement.BatchId).HasColumnName("batch_id").HasColumnType("uuid");
            builder.Property(movement => movement.Type)
                .HasColumnName("type").HasColumnType("text").HasConversion<string>();
            builder.Property(movement => movement.Quantity)
                .HasColumnName("quantity").HasPrecision(12, 3);
            builder.Property(movement => movement.Reason).HasColumnName("reason").HasColumnType("text");
            builder.Property(movement => movement.IdempotencyKey)
                .HasColumnName("idempotency_key").HasColumnType("text");
            builder.Property(movement => movement.CreatedAt)
                .HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(movement => movement.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");

            builder.HasIndex(movement => new
            {
                movement.BranchId,
                movement.IdempotencyKey,
                movement.BatchId,
            })
                .HasDatabaseName("uq_stock_movements_branch_key_batch")
                .IsUnique();
            builder.HasIndex(movement => movement.ItemId).HasDatabaseName("ix_stock_movements_item_id");
            builder.HasIndex(movement => movement.BranchId).HasDatabaseName("ix_stock_movements_branch_id");
            builder.HasIndex(movement => movement.BatchId).HasDatabaseName("ix_stock_movements_batch_id");
            builder.HasIndex(movement => movement.CreatedBy).HasDatabaseName("ix_stock_movements_created_by");

            builder.HasOne<InventoryItem>()
                .WithMany()
                .HasForeignKey(movement => movement.ItemId)
                .HasConstraintName("fk_stock_movements_item_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(movement => movement.BranchId)
                .HasConstraintName("fk_stock_movements_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<InventoryBatch>()
                .WithMany()
                .HasForeignKey(movement => movement.BatchId)
                .HasConstraintName("fk_stock_movements_batch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(movement => movement.CreatedBy)
                .HasConstraintName("fk_stock_movements_created_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
