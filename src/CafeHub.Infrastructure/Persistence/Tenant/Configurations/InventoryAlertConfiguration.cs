using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class InventoryAlertConfiguration : IEntityTypeConfiguration<InventoryAlert>
    {
        public void Configure(EntityTypeBuilder<InventoryAlert> builder)
        {
            builder.ToTable("inventory_alerts", table =>
            {
                table.HasCheckConstraint(
                    "ck_inventory_alerts_kind",
                    "kind IN ('LowStock', 'NearExpiry', 'Expired')");
                table.HasCheckConstraint(
                    "ck_inventory_alerts_state",
                    "state IN ('Open', 'Resolved')");
                table.HasCheckConstraint(
                    "ck_inventory_alerts_batch",
                    "((kind = 'LowStock' AND batch_id IS NULL) OR (kind IN ('NearExpiry', 'Expired') AND batch_id IS NOT NULL))");
                table.HasCheckConstraint(
                    "ck_inventory_alerts_resolved",
                    "((state = 'Open' AND resolved_at IS NULL) OR (state = 'Resolved' AND resolved_at IS NOT NULL))");
                table.HasCheckConstraint(
                    "ck_inventory_alerts_resolved_not_before_raised",
                    "resolved_at IS NULL OR resolved_at >= raised_at");
                table.HasCheckConstraint(
                    "ck_inventory_alerts_notified_not_before_raised",
                    "notified_at IS NULL OR notified_at >= raised_at");
            });

            builder.HasKey(alert => alert.Id).HasName("pk_inventory_alerts");
            builder.Property(alert => alert.Id)
                .HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(alert => alert.ItemId).HasColumnName("item_id").HasColumnType("uuid");
            builder.Property(alert => alert.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(alert => alert.BatchId).HasColumnName("batch_id").HasColumnType("uuid");
            builder.Property(alert => alert.Kind)
                .HasColumnName("kind").HasColumnType("text").HasConversion<string>();
            builder.Property(alert => alert.State)
                .HasColumnName("state").HasColumnType("text").HasConversion<string>();
            builder.Property(alert => alert.RaisedAt)
                .HasColumnName("raised_at").HasColumnType("timestamptz");
            builder.Property(alert => alert.ResolvedAt)
                .HasColumnName("resolved_at").HasColumnType("timestamptz");
            builder.Property(alert => alert.NotifiedAt)
                .HasColumnName("notified_at").HasColumnType("timestamptz");
            builder.Property(alert => alert.CreatedAt)
                .HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(alert => alert.UpdatedAt)
                .HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(alert => alert.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(alert => alert.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(alert => new { alert.ItemId, alert.BranchId, alert.Kind, alert.BatchId })
                .HasDatabaseName("uq_inventory_alerts_open_batch")
                .HasFilter("state = 'Open' AND batch_id IS NOT NULL")
                .IsUnique();
            builder.HasIndex(alert => new { alert.ItemId, alert.BranchId, alert.Kind })
                .HasDatabaseName("uq_inventory_alerts_open_item")
                .HasFilter("state = 'Open' AND batch_id IS NULL")
                .IsUnique();
            builder.HasIndex(alert => alert.ItemId).HasDatabaseName("ix_inventory_alerts_item_id");
            builder.HasIndex(alert => alert.BranchId).HasDatabaseName("ix_inventory_alerts_branch_id");
            builder.HasIndex(alert => alert.BatchId).HasDatabaseName("ix_inventory_alerts_batch_id");
            builder.HasIndex(alert => alert.CreatedBy).HasDatabaseName("ix_inventory_alerts_created_by");
            builder.HasIndex(alert => alert.UpdatedBy).HasDatabaseName("ix_inventory_alerts_updated_by");

            builder.HasOne<InventoryItem>()
                .WithMany()
                .HasForeignKey(alert => alert.ItemId)
                .HasConstraintName("fk_inventory_alerts_item_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(alert => alert.BranchId)
                .HasConstraintName("fk_inventory_alerts_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<InventoryBatch>()
                .WithMany()
                .HasForeignKey(alert => alert.BatchId)
                .HasConstraintName("fk_inventory_alerts_batch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(alert => alert.CreatedBy)
                .HasConstraintName("fk_inventory_alerts_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(alert => alert.UpdatedBy)
                .HasConstraintName("fk_inventory_alerts_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
