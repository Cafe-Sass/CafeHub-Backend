using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class ProductStationAssignmentConfiguration : IEntityTypeConfiguration<ProductStationAssignment>
    {
        public void Configure(EntityTypeBuilder<ProductStationAssignment> builder)
        {
            builder.ToTable("product_station_assignments");
            builder.HasKey(row => row.Id).HasName("pk_product_station_assignments");
            builder.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(row => row.ProductId).HasColumnName("product_id");
            builder.Property(row => row.BranchId).HasColumnName("branch_id");
            builder.Property(row => row.StationId).HasColumnName("station_id");
            builder.Property(row => row.StartedAt).HasColumnName("started_at");
            builder.Property(row => row.StartedBy).HasColumnName("started_by");
            builder.Property(row => row.EndedAt).HasColumnName("ended_at");
            builder.Property(row => row.EndedBy).HasColumnName("ended_by");

            builder.HasIndex(row => new { row.ProductId, row.BranchId }, "ProductBranchHistory")
                .HasDatabaseName("ix_product_station_assignments_slot");
            builder.HasIndex(row => new { row.ProductId, row.BranchId }, "CurrentProductBranchAssignment")
                .IsUnique().HasFilter("ended_at IS NULL").HasDatabaseName("uq_product_station_assignments_current");
            builder.HasIndex(row => new { row.BranchId, row.StationId }).HasDatabaseName("ix_product_station_assignments_station");
            builder.HasIndex(row => row.StartedBy).HasDatabaseName("ix_product_station_assignments_started_by");
            builder.HasIndex(row => row.EndedBy).HasDatabaseName("ix_product_station_assignments_ended_by");

            builder.HasOne<ProductBranchStationSlot>().WithMany().HasForeignKey(row => new { row.ProductId, row.BranchId })
                .HasConstraintName("fk_product_station_assignments_slot").OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Station>().WithMany().HasForeignKey(row => new { row.BranchId, row.StationId })
                .HasPrincipalKey(station => new { station.BranchId, station.Id })
                .HasConstraintName("fk_product_station_assignments_station").OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>().WithMany().HasForeignKey(row => row.StartedBy)
                .HasConstraintName("fk_product_station_assignments_started_by").OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>().WithMany().HasForeignKey(row => row.EndedBy)
                .HasConstraintName("fk_product_station_assignments_ended_by").OnDelete(DeleteBehavior.Restrict);
        }
    }
}
