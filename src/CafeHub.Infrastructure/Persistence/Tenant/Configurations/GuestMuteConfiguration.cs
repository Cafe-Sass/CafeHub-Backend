using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class GuestMuteConfiguration : IEntityTypeConfiguration<GuestMute>
    {
        public void Configure(EntityTypeBuilder<GuestMute> builder)
        {
            builder.ToTable("guest_mutes");

            builder.HasKey(mute => mute.Id).HasName("pk_guest_mutes");
            builder.Property(mute => mute.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(mute => mute.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(mute => mute.OrderId).HasColumnName("order_id").HasColumnType("uuid");
            builder.Property(mute => mute.DeviceId).HasColumnName("device_id").HasColumnType("text");
            builder.Property(mute => mute.MutedByUserId).HasColumnName("muted_by_user_id").HasColumnType("uuid");
            builder.Property(mute => mute.Reason).HasColumnName("reason").HasColumnType("text");
            builder.Property(mute => mute.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz");
            builder.Property(mute => mute.LiftedAt).HasColumnName("lifted_at").HasColumnType("timestamptz");
            builder.Property(mute => mute.LiftedByUserId).HasColumnName("lifted_by_user_id").HasColumnType("uuid");
            builder.Property(mute => mute.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(mute => mute.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(mute => mute.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(mute => mute.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(mute => new { mute.BranchId, mute.DeviceId })
                .HasDatabaseName("ix_guest_mutes_branch_device");
            builder.HasIndex(mute => mute.MutedByUserId)
                .HasDatabaseName("ix_guest_mutes_muted_by_user_id");
            builder.HasIndex(mute => mute.LiftedByUserId)
                .HasDatabaseName("ix_guest_mutes_lifted_by_user_id");
            builder.HasIndex(mute => mute.OrderId)
                .HasDatabaseName("ix_guest_mutes_order_id");
            builder.HasIndex(mute => mute.CreatedBy)
                .HasDatabaseName("ix_guest_mutes_created_by");
            builder.HasIndex(mute => mute.UpdatedBy)
                .HasDatabaseName("ix_guest_mutes_updated_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(mute => mute.BranchId)
                .HasConstraintName("fk_guest_mutes_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(mute => mute.OrderId)
                .HasConstraintName("fk_guest_mutes_order_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(mute => mute.MutedByUserId)
                .HasConstraintName("fk_guest_mutes_muted_by_user_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(mute => mute.LiftedByUserId)
                .HasConstraintName("fk_guest_mutes_lifted_by_user_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(mute => mute.CreatedBy)
                .HasConstraintName("fk_guest_mutes_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(mute => mute.UpdatedBy)
                .HasConstraintName("fk_guest_mutes_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
