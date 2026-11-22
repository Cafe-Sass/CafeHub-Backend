using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class OrderResponseMissHolderConfiguration : IEntityTypeConfiguration<OrderResponseMissHolder>
    {
        public void Configure(EntityTypeBuilder<OrderResponseMissHolder> builder)
        {
            builder.ToTable("order_response_miss_holders");

            builder.HasKey(holder => new { holder.OrderResponseMissId, holder.UserId })
                .HasName("pk_order_response_miss_holders");
            builder.Property(holder => holder.OrderResponseMissId).HasColumnName("order_response_miss_id").HasColumnType("uuid");
            builder.Property(holder => holder.UserId).HasColumnName("user_id").HasColumnType("uuid");
            builder.Property(holder => holder.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            builder.HasIndex(holder => holder.UserId)
                .HasDatabaseName("ix_order_response_miss_holders_user_id");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(holder => holder.UserId)
                .HasConstraintName("fk_order_response_miss_holders_user_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
