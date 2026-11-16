using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("products", table =>
            {
                table.HasCheckConstraint("ck_products_name_length", "char_length(name) BETWEEN 1 AND 96");
                table.HasCheckConstraint("ck_products_description_length", "description IS NULL OR (length(description) >= 1 AND length(description) <= 1000)");
                table.HasCheckConstraint("ck_products_ingredients_length", "ingredients IS NULL OR (length(ingredients) >= 1 AND length(ingredients) <= 1000)");
                table.HasCheckConstraint("ck_products_kind", "kind IN ('Food', 'Drink')");
                table.HasCheckConstraint("ck_products_preparation_minutes", "preparation_minutes IS NULL OR (preparation_minutes >= 0 AND preparation_minutes <= 240)");
                table.HasCheckConstraint("ck_products_sort_order", "sort_order >= 0");
                table.HasCheckConstraint("ck_products_status", "status IN ('Active', 'Inactive')");
            });

            builder.HasKey(product => product.Id).HasName("pk_products");
            builder.Property(product => product.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(product => product.CategoryId).HasColumnName("category_id").HasColumnType("uuid");
            builder.Property(product => product.StationId).HasColumnName("station_id").HasColumnType("uuid");
            builder.Property(product => product.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(product => product.Description).HasColumnName("description").HasColumnType("text");
            builder.Property(product => product.Ingredients).HasColumnName("ingredients").HasColumnType("text");
            builder.Property(product => product.Kind).HasColumnName("kind").HasColumnType("text").HasConversion<string>();
            builder.Property(product => product.Caffeine).HasColumnName("caffeine").HasColumnType("boolean");
            builder.Property(product => product.PreparationMinutes).HasColumnName("preparation_minutes").HasColumnType("integer");
            builder.Property(product => product.SortOrder).HasColumnName("sort_order").HasColumnType("integer");
            builder.Property(product => product.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(product => product.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(product => product.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(product => product.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(product => product.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(product => product.CategoryId).HasDatabaseName("ix_products_category_id");
            builder.HasIndex(product => product.StationId).HasDatabaseName("ix_products_station_id");
            builder.HasIndex(product => product.CreatedBy).HasDatabaseName("ix_products_created_by");
            builder.HasIndex(product => product.UpdatedBy).HasDatabaseName("ix_products_updated_by");

            builder.HasOne<MenuCategory>()
                .WithMany()
                .HasForeignKey(product => product.CategoryId)
                .HasConstraintName("fk_products_category_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Station>()
                .WithMany()
                .HasForeignKey(product => product.StationId)
                .HasConstraintName("fk_products_station_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(product => product.CreatedBy)
                .HasConstraintName("fk_products_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(product => product.UpdatedBy)
                .HasConstraintName("fk_products_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(product => product.Sizes)
                .WithOne()
                .HasForeignKey(size => size.ProductId)
                .HasConstraintName("fk_product_sizes_product_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(product => product.Addons)
                .WithOne()
                .HasForeignKey(addon => addon.ProductId)
                .HasConstraintName("fk_product_addons_product_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(product => product.Tags)
                .WithOne()
                .HasForeignKey(tag => tag.ProductId)
                .HasConstraintName("fk_product_tags_product_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
