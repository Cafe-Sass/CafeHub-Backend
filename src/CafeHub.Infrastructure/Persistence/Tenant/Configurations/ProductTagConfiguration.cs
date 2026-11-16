using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class ProductTagConfiguration : IEntityTypeConfiguration<ProductTag>
    {
        public void Configure(EntityTypeBuilder<ProductTag> builder)
        {
            builder.ToTable("product_tags", table =>
            {
                table.HasCheckConstraint("ck_product_tags_tag_length", "char_length(tag) BETWEEN 1 AND 32");
                table.HasCheckConstraint("ck_product_tags_tag_lowercase", "tag = lower(tag)");
                table.HasCheckConstraint("ck_product_tags_tag_format", "tag ~ '^[a-z0-9 -]+$'");
            });

            builder.HasKey(tag => new { tag.ProductId, tag.Tag }).HasName("pk_product_tags");
            builder.Property(tag => tag.ProductId).HasColumnName("product_id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(tag => tag.Tag).HasColumnName("tag").HasColumnType("text");
            builder.Property(tag => tag.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            builder.HasIndex(tag => tag.Tag).HasDatabaseName("ix_product_tags_tag");

            builder.HasOne<Product>()
                .WithMany(product => product.Tags)
                .HasForeignKey(tag => tag.ProductId)
                .HasConstraintName("fk_product_tags_product_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
