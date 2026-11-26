using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class AttendanceDeviceCodeConfiguration : IEntityTypeConfiguration<AttendanceDeviceCode>
    {
        public void Configure(EntityTypeBuilder<AttendanceDeviceCode> builder)
        {
            builder.ToTable("attendance_device_codes", table =>
            {
                table.HasCheckConstraint(
                    "ck_attendance_device_codes_value",
                    "char_length(device_code) BETWEEN 1 AND 64 AND device_code !~ '[[:cntrl:]]'");
            });

            builder.HasKey(code => code.Id).HasName("pk_attendance_device_codes");
            builder.Property(code => code.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(code => code.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(code => code.StaffId).HasColumnName("staff_id").HasColumnType("uuid");
            builder.Property(code => code.DeviceCode).HasColumnName("device_code").HasColumnType("text");
            builder.Property(code => code.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(code => code.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(code => code.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(code => code.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(code => code.BranchId).HasDatabaseName("ix_attendance_device_codes_branch_id");
            builder.HasIndex(code => code.StaffId).HasDatabaseName("ix_attendance_device_codes_staff_id");
            builder.HasIndex(code => new { code.BranchId, code.DeviceCode })
                .HasDatabaseName("uq_attendance_device_codes_branch_code")
                .IsUnique();
            builder.HasIndex(code => new { code.BranchId, code.StaffId })
                .HasDatabaseName("uq_attendance_device_codes_branch_staff")
                .IsUnique();
            builder.HasIndex(code => code.CreatedBy).HasDatabaseName("ix_attendance_device_codes_created_by");
            builder.HasIndex(code => code.UpdatedBy).HasDatabaseName("ix_attendance_device_codes_updated_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(code => code.BranchId)
                .HasConstraintName("fk_attendance_device_codes_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(code => code.StaffId)
                .HasConstraintName("fk_attendance_device_codes_staff_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(code => code.CreatedBy)
                .HasConstraintName("fk_attendance_device_codes_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(code => code.UpdatedBy)
                .HasConstraintName("fk_attendance_device_codes_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
