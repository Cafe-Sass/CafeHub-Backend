using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class OrderItemAddonConfiguration : IEntityTypeConfiguration<OrderItemAddon>
    {
        public void Configure(EntityTypeBuilder<OrderItemAddon> builder)
        {
            builder.ToTable("order_item_addons", table =>
            {
                table.HasCheckConstraint(
                    "ck_order_item_addons_name_length", "char_length(addon_name) BETWEEN 1 AND 48");
                table.HasCheckConstraint(
                    "ck_order_item_addons_price_range", "price >= 0 AND price <= 99999999.99");
            });

            builder.HasKey(addon => addon.Id).HasName("pk_order_item_addons");
            builder.Property(addon => addon.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(addon => addon.OrderItemId).HasColumnName("order_item_id").HasColumnType("uuid");
            builder.Property(addon => addon.ProductAddonId).HasColumnName("product_addon_id").HasColumnType("uuid");
            builder.Property(addon => addon.AddonName).HasColumnName("addon_name").HasColumnType("text");
            builder.Property(addon => addon.Price).HasColumnName("price").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(addon => addon.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(addon => addon.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(addon => addon.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(addon => addon.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(addon => addon.OrderItemId).HasDatabaseName("ix_order_item_addons_order_item_id");
            builder.HasIndex(addon => addon.ProductAddonId).HasDatabaseName("ix_order_item_addons_product_addon_id");
            builder.HasIndex(addon => addon.CreatedBy).HasDatabaseName("ix_order_item_addons_created_by");
            builder.HasIndex(addon => addon.UpdatedBy).HasDatabaseName("ix_order_item_addons_updated_by");

            builder.HasOne<ProductAddon>()
                .WithMany()
                .HasForeignKey(addon => addon.ProductAddonId)
                .HasConstraintName("fk_order_item_addons_product_addon_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(addon => addon.CreatedBy)
                .HasConstraintName("fk_order_item_addons_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(addon => addon.UpdatedBy)
                .HasConstraintName("fk_order_item_addons_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
