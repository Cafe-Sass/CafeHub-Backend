using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
    {
        public void Configure(EntityTypeBuilder<MenuCategory> builder)
        {
            builder.ToTable("menu_categories", table =>
            {
                table.HasCheckConstraint("ck_menu_categories_parent_not_self", "parent_id <> id");
                table.HasCheckConstraint("ck_menu_categories_name_length", "char_length(name) BETWEEN 1 AND 64");
                table.HasCheckConstraint("ck_menu_categories_sort_order", "sort_order >= 0");
                table.HasCheckConstraint("ck_menu_categories_status", "status IN ('Active', 'Inactive')");
            });

            builder.HasKey(category => category.Id).HasName("pk_menu_categories");
            builder.Property(category => category.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(category => category.ParentId).HasColumnName("parent_id").HasColumnType("uuid");
            builder.Property(category => category.Name).HasColumnName("name").HasColumnType("text").IsRequired();
            builder.Property(category => category.SortOrder).HasColumnName("sort_order").HasColumnType("integer");
            builder.Property(category => category.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(category => category.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(category => category.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(category => category.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(category => category.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(category => new { category.ParentId, category.SortOrder, category.Id })
                .HasDatabaseName("ix_menu_categories_parent_id_sort_order_id");
            builder.HasIndex(category => category.CreatedBy).HasDatabaseName("ix_menu_categories_created_by");
            builder.HasIndex(category => category.UpdatedBy).HasDatabaseName("ix_menu_categories_updated_by");

            builder.HasOne(category => category.Parent)
                .WithMany()
                .HasForeignKey(category => category.ParentId)
                .HasConstraintName("fk_menu_categories_parent_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(category => category.CreatedBy)
                .HasConstraintName("fk_menu_categories_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(category => category.UpdatedBy)
                .HasConstraintName("fk_menu_categories_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
