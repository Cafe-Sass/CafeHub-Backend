using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class AttendanceDeviceConfiguration : IEntityTypeConfiguration<AttendanceDevice>
    {
        public void Configure(EntityTypeBuilder<AttendanceDevice> builder)
        {
            builder.ToTable("attendance_devices", table =>
            {
                table.HasCheckConstraint(
                    "ck_attendance_devices_name",
                    "char_length(name) BETWEEN 1 AND 64 AND name !~ '[[:cntrl:]]'");
                table.HasCheckConstraint(
                    "ck_attendance_devices_key_hash",
                    "char_length(key_hash) = 64");
                table.HasCheckConstraint(
                    "ck_attendance_devices_status",
                    "status IN ('Active', 'Revoked')");
            });

            builder.HasKey(device => device.Id).HasName("pk_attendance_devices");
            builder.Property(device => device.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(device => device.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(device => device.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(device => device.KeyHash).HasColumnName("key_hash").HasColumnType("text");
            builder.Property(device => device.Status)
                .HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(device => device.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamptz");
            builder.Property(device => device.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(device => device.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(device => device.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(device => device.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(device => device.BranchId).HasDatabaseName("ix_attendance_devices_branch_id");
            builder.HasIndex(device => device.KeyHash)
                .HasDatabaseName("uq_attendance_devices_key_hash")
                .IsUnique();
            builder.HasIndex(device => device.CreatedBy).HasDatabaseName("ix_attendance_devices_created_by");
            builder.HasIndex(device => device.UpdatedBy).HasDatabaseName("ix_attendance_devices_updated_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(device => device.BranchId)
                .HasConstraintName("fk_attendance_devices_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(device => device.CreatedBy)
                .HasConstraintName("fk_attendance_devices_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(device => device.UpdatedBy)
                .HasConstraintName("fk_attendance_devices_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
