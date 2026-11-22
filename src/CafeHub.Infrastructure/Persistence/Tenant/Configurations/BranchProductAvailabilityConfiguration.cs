using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class BranchProductAvailabilityConfiguration : IEntityTypeConfiguration<BranchProductAvailability>
    {
        public void Configure(EntityTypeBuilder<BranchProductAvailability> builder)
        {
            builder.ToTable("branch_product_availability", table =>
            {
                table.HasCheckConstraint(
                    "ck_branch_product_availability_reason_code",
                    "reason_code IN ('manual', 'out_of_stock', 'expired')");
            });

            builder.HasKey(row => new { row.BranchId, row.ProductId }).HasName("pk_branch_product_availability");
            builder.Property(row => row.BranchId).HasColumnName("branch_id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(row => row.ProductId).HasColumnName("product_id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(row => row.IsAvailable).HasColumnName("is_available").HasColumnType("boolean");
            builder.Property(row => row.ReasonCode).HasColumnName("reason_code").HasColumnType("text");
            builder.Property(row => row.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(row => row.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(row => row.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(row => row.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(row => row.ProductId).HasDatabaseName("ix_branch_product_availability_product_id");
            builder.HasIndex(row => row.CreatedBy).HasDatabaseName("ix_branch_product_availability_created_by");
            builder.HasIndex(row => row.UpdatedBy).HasDatabaseName("ix_branch_product_availability_updated_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(row => row.BranchId)
                .HasConstraintName("fk_branch_product_availability_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(row => row.ProductId)
                .HasConstraintName("fk_branch_product_availability_product_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(row => row.CreatedBy)
                .HasConstraintName("fk_branch_product_availability_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(row => row.UpdatedBy)
                .HasConstraintName("fk_branch_product_availability_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
