using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
    {
        public void Configure(EntityTypeBuilder<InventoryItem> builder)
        {
            builder.ToTable("inventory_items", table =>
            {
                table.HasCheckConstraint(
                    "ck_inventory_items_name",
                    "char_length(name) BETWEEN 1 AND 96 AND name !~ '[[:cntrl:]]'");
                table.HasCheckConstraint(
                    "ck_inventory_items_unit",
                    "unit IN ('Kg', 'G', 'L', 'Ml', 'Piece', 'Pack')");
                table.HasCheckConstraint(
                    "ck_inventory_items_low_stock_threshold",
                    "low_stock_threshold >= 0");
                table.HasCheckConstraint(
                    "ck_inventory_items_near_expiry_days",
                    "near_expiry_days >= 0");
                table.HasCheckConstraint(
                    "ck_inventory_items_status",
                    "status IN ('Active', 'Inactive')");
            });

            builder.HasKey(item => item.Id).HasName("pk_inventory_items");
            builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(item => item.Name).HasColumnName("name").HasColumnType("text").IsRequired();
            builder.Property(item => item.Unit)
                .HasColumnName("unit").HasColumnType("text").HasConversion<string>();
            builder.Property(item => item.LowStockThreshold)
                .HasColumnName("low_stock_threshold").HasPrecision(12, 3);
            builder.Property(item => item.TracksExpiry).HasColumnName("tracks_expiry").HasColumnType("boolean");
            builder.Property(item => item.NearExpiryDays)
                .HasColumnName("near_expiry_days")
                .HasColumnType("integer")
                .HasDefaultValue(InventoryItem.DefaultNearExpiryDays);
            builder.Property(item => item.Status)
                .HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(item => item.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(item => item.CreatedBy).HasDatabaseName("ix_inventory_items_created_by");
            builder.HasIndex(item => item.UpdatedBy).HasDatabaseName("ix_inventory_items_updated_by");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(item => item.CreatedBy)
                .HasConstraintName("fk_inventory_items_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(item => item.UpdatedBy)
                .HasConstraintName("fk_inventory_items_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
