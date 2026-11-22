using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class OrderResponseMissConfiguration : IEntityTypeConfiguration<OrderResponseMiss>
    {
        public void Configure(EntityTypeBuilder<OrderResponseMiss> builder)
        {
            builder.ToTable("order_response_misses");

            builder.HasKey(miss => miss.Id).HasName("pk_order_response_misses");
            builder.Property(miss => miss.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(miss => miss.OrderId).HasColumnName("order_id").HasColumnType("uuid");
            builder.Property(miss => miss.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(miss => miss.ExpiredAt).HasColumnName("expired_at").HasColumnType("timestamptz");
            builder.Property(miss => miss.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(miss => miss.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(miss => miss.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(miss => miss.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(miss => miss.OrderId)
                .HasDatabaseName("uq_order_response_misses_order_id")
                .IsUnique();
            builder.HasIndex(miss => miss.CreatedBy)
                .HasDatabaseName("ix_order_response_misses_created_by");
            builder.HasIndex(miss => miss.UpdatedBy)
                .HasDatabaseName("ix_order_response_misses_updated_by");

            builder.HasIndex(miss => new { miss.BranchId, miss.ExpiredAt })
                .HasDatabaseName("ix_order_response_misses_branch_expired_at");

            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(miss => miss.OrderId)
                .HasConstraintName("fk_order_response_misses_order_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(miss => miss.BranchId)
                .HasConstraintName("fk_order_response_misses_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(miss => miss.CreatedBy)
                .HasConstraintName("fk_order_response_misses_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(miss => miss.UpdatedBy)
                .HasConstraintName("fk_order_response_misses_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(miss => miss.Holders)
                .WithOne()
                .HasForeignKey(holder => holder.OrderResponseMissId)
                .HasConstraintName("fk_order_response_miss_holders_miss_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
