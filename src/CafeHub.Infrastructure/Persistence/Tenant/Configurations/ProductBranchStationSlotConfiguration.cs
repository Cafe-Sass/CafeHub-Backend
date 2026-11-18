using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class ProductBranchStationSlotConfiguration : IEntityTypeConfiguration<ProductBranchStationSlot>
    {
        public void Configure(EntityTypeBuilder<ProductBranchStationSlot> builder)
        {
            builder.ToTable("product_branch_station_slots");
            builder.HasKey(slot => new { slot.ProductId, slot.BranchId }).HasName("pk_product_branch_station_slots");
            builder.Property(slot => slot.ProductId).HasColumnName("product_id");
            builder.Property(slot => slot.BranchId).HasColumnName("branch_id");
            builder.Property(slot => slot.UpdatedAt).HasColumnName("updated_at");
            builder.Property<uint>("xmin").IsRowVersion();
            builder.HasIndex(slot => slot.BranchId).HasDatabaseName("ix_product_branch_station_slots_branch_id");
            builder.HasOne<Product>().WithMany().HasForeignKey(slot => slot.ProductId)
                .HasConstraintName("fk_product_branch_station_slots_product_id").OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Branch>().WithMany().HasForeignKey(slot => slot.BranchId)
                .HasConstraintName("fk_product_branch_station_slots_branch_id").OnDelete(DeleteBehavior.Restrict);
        }
    }
}
