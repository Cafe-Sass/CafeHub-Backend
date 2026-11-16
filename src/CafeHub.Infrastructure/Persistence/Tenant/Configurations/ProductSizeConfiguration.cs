using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class ProductSizeConfiguration : IEntityTypeConfiguration<ProductSize>
    {
        public void Configure(EntityTypeBuilder<ProductSize> builder)
        {
            builder.ToTable("product_sizes", table =>
            {
                table.HasCheckConstraint("ck_product_sizes_name_length", "char_length(name) BETWEEN 1 AND 32");
                table.HasCheckConstraint("ck_product_sizes_price_range", "price >= 0 AND price <= 99999999.99");
                table.HasCheckConstraint("ck_product_sizes_status", "status IN ('Active', 'Inactive')");
            });

            builder.HasKey(size => size.Id).HasName("pk_product_sizes");
            builder.Property(size => size.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(size => size.ProductId).HasColumnName("product_id").HasColumnType("uuid");
            builder.Property(size => size.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(size => size.Price).HasColumnName("price").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(size => size.IsDefault).HasColumnName("is_default").HasColumnType("boolean");
            builder.Property(size => size.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(size => size.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(size => size.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(size => size.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(size => size.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(size => size.ProductId).HasDatabaseName("ix_product_sizes_product_id");
            builder.HasIndex(size => size.CreatedBy).HasDatabaseName("ix_product_sizes_created_by");
            builder.HasIndex(size => size.UpdatedBy).HasDatabaseName("ix_product_sizes_updated_by");

            builder.HasOne<Product>()
                .WithMany(product => product.Sizes)
                .HasForeignKey(size => size.ProductId)
                .HasConstraintName("fk_product_sizes_product_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(size => size.CreatedBy)
                .HasConstraintName("fk_product_sizes_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(size => size.UpdatedBy)
                .HasConstraintName("fk_product_sizes_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
