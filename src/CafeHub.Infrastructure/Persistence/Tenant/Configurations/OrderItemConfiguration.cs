using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("order_items", table =>
            {
                table.HasCheckConstraint("ck_order_items_line_number", "line_number >= 1");
                table.HasCheckConstraint(
                    "ck_order_items_product_name_length", "char_length(product_name) BETWEEN 1 AND 96");
                table.HasCheckConstraint("ck_order_items_size_name_length", "char_length(size_name) BETWEEN 1 AND 32");
                table.HasCheckConstraint(
                    "ck_order_items_unit_price_range", "unit_price >= 0 AND unit_price <= 99999999.99");
                table.HasCheckConstraint("ck_order_items_quantity_range", "quantity BETWEEN 1 AND 50");
                table.HasCheckConstraint(
                    "ck_order_items_line_total_range", "line_total >= 0 AND line_total <= 99999999.99");
                table.HasCheckConstraint(
                    "ck_order_items_product_or_wifi", "(product_id IS NULL) <> (wifi_package_id IS NULL)");
                table.HasCheckConstraint(
                    "ck_order_items_wifi_snapshot",
                    "wifi_package_id IS NULL OR (wifi_duration_minutes IS NOT NULL AND wifi_duration_minutes BETWEEN 1 AND 43200)");
            });

            builder.HasKey(item => item.Id).HasName("pk_order_items");
            builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(item => item.OrderId).HasColumnName("order_id").HasColumnType("uuid");
            builder.Property(item => item.LineNumber).HasColumnName("line_number").HasColumnType("integer");
            builder.Property(item => item.ProductId).HasColumnName("product_id").HasColumnType("uuid");
            builder.Property(item => item.ProductSizeId).HasColumnName("product_size_id").HasColumnType("uuid");
            builder.Property(item => item.WifiPackageId).HasColumnName("wifi_package_id").HasColumnType("uuid");
            builder.Property(item => item.WifiDurationMinutes).HasColumnName("wifi_duration_minutes").HasColumnType("integer");
            builder.Property(item => item.WifiSpeedMbps).HasColumnName("wifi_speed_mbps").HasColumnType("numeric(9,2)").HasPrecision(9, 2);
            builder.Property(item => item.StationId).HasColumnName("station_id").HasColumnType("uuid");
            builder.Property(item => item.ProductName).HasColumnName("product_name").HasColumnType("text");
            builder.Property(item => item.SizeName).HasColumnName("size_name").HasColumnType("text");
            builder.Property(item => item.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(item => item.Quantity).HasColumnName("quantity").HasColumnType("integer");
            builder.Property(item => item.LineTotal).HasColumnName("line_total").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(item => item.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(item => item.OrderId).HasDatabaseName("ix_order_items_order_id");
            builder.HasIndex(item => new { item.OrderId, item.LineNumber })
                .HasDatabaseName("uq_order_items_order_id_line_number")
                .IsUnique();
            builder.HasIndex(item => item.ProductId).HasDatabaseName("ix_order_items_product_id");
            builder.HasIndex(item => item.ProductSizeId).HasDatabaseName("ix_order_items_product_size_id");
            builder.HasIndex(item => item.WifiPackageId).HasDatabaseName("ix_order_items_wifi_package_id");
            builder.HasIndex(item => item.StationId).HasDatabaseName("ix_order_items_station_id");
            builder.HasIndex(item => item.CreatedBy).HasDatabaseName("ix_order_items_created_by");
            builder.HasIndex(item => item.UpdatedBy).HasDatabaseName("ix_order_items_updated_by");

            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .HasConstraintName("fk_order_items_product_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ProductSize>()
                .WithMany()
                .HasForeignKey(item => item.ProductSizeId)
                .HasConstraintName("fk_order_items_product_size_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<WifiPackage>()
                .WithMany()
                .HasForeignKey(item => item.WifiPackageId)
                .HasConstraintName("fk_order_items_wifi_package_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Station>()
                .WithMany()
                .HasForeignKey(item => item.StationId)
                .HasConstraintName("fk_order_items_station_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(item => item.CreatedBy)
                .HasConstraintName("fk_order_items_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(item => item.UpdatedBy)
                .HasConstraintName("fk_order_items_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(item => item.Addons)
                .WithOne()
                .HasForeignKey(addon => addon.OrderItemId)
                .HasConstraintName("fk_order_item_addons_order_item_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
