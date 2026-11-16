using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class ProductAddonConfiguration : IEntityTypeConfiguration<ProductAddon>
    {
        public void Configure(EntityTypeBuilder<ProductAddon> builder)
        {
            builder.ToTable("product_addons", table =>
            {
                table.HasCheckConstraint("ck_product_addons_name_length", "char_length(name) BETWEEN 1 AND 48");
                table.HasCheckConstraint("ck_product_addons_price_range", "price >= 0 AND price <= 99999999.99");
                table.HasCheckConstraint("ck_product_addons_status", "status IN ('Active', 'Inactive')");
            });

            builder.HasKey(addon => addon.Id).HasName("pk_product_addons");
            builder.Property(addon => addon.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(addon => addon.ProductId).HasColumnName("product_id").HasColumnType("uuid");
            builder.Property(addon => addon.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(addon => addon.Price).HasColumnName("price").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(addon => addon.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(addon => addon.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(addon => addon.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(addon => addon.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(addon => addon.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(addon => addon.ProductId).HasDatabaseName("ix_product_addons_product_id");
            builder.HasIndex(addon => addon.CreatedBy).HasDatabaseName("ix_product_addons_created_by");
            builder.HasIndex(addon => addon.UpdatedBy).HasDatabaseName("ix_product_addons_updated_by");

            builder.HasOne<Product>()
                .WithMany(product => product.Addons)
                .HasForeignKey(addon => addon.ProductId)
                .HasConstraintName("fk_product_addons_product_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(addon => addon.CreatedBy)
                .HasConstraintName("fk_product_addons_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(addon => addon.UpdatedBy)
                .HasConstraintName("fk_product_addons_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
