using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
    {
        public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
        {
            builder.ToTable("attendance_records", table =>
            {
                table.HasCheckConstraint(
                    "ck_attendance_records_window",
                    "check_out_at IS NULL OR check_out_at > check_in_at");
                table.HasCheckConstraint(
                    "ck_attendance_records_source", "source IN ('Qr', 'Device')");
                table.HasCheckConstraint(
                    "ck_attendance_records_source_shape",
                    "(source = 'Qr' AND check_in_session_id IS NOT NULL) OR (source = 'Device' AND check_in_session_id IS NULL AND check_out_session_id IS NULL)");
            });

            builder.HasKey(record => record.Id).HasName("pk_attendance_records");
            builder.Property(record => record.Id)
                .HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(record => record.BranchId)
                .HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(record => record.StaffId)
                .HasColumnName("staff_id").HasColumnType("uuid");
            builder.Property(record => record.CheckInAt)
                .HasColumnName("check_in_at").HasColumnType("timestamptz");
            builder.Property(record => record.CheckOutAt)
                .HasColumnName("check_out_at").HasColumnType("timestamptz");
            builder.Property(record => record.CheckInSessionId)
                .HasColumnName("check_in_session_id").HasColumnType("uuid");
            builder.Property(record => record.CheckOutSessionId)
                .HasColumnName("check_out_session_id").HasColumnType("uuid");
            builder.Property(record => record.Source)
                .HasColumnName("source").HasColumnType("text").HasConversion<string>()
                .HasDefaultValue(AttendanceSource.Qr).IsRequired();
            builder.Property(record => record.CreatedAt)
                .HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(record => record.UpdatedAt)
                .HasColumnName("updated_at").HasColumnType("timestamptz");

            builder.HasIndex(record => new { record.StaffId, record.CheckInSessionId })
                .HasDatabaseName("uq_attendance_records_staff_check_in_session")
                .IsUnique();
            builder.HasIndex(record => new { record.StaffId, record.CheckOutSessionId })
                .HasDatabaseName("uq_attendance_records_staff_check_out_session")
                .IsUnique()
                .HasFilter("check_out_session_id IS NOT NULL");
            builder.HasIndex(record => record.StaffId)
                .HasDatabaseName("uq_attendance_records_open_staff")
                .IsUnique()
                .HasFilter("check_out_at IS NULL");
            builder.HasIndex(record => record.CheckInSessionId)
                .HasDatabaseName("ix_attendance_records_check_in_session_id");
            builder.HasIndex(record => record.CheckOutSessionId)
                .HasDatabaseName("ix_attendance_records_check_out_session_id");
            builder.HasIndex(record => new { record.BranchId, record.CheckInAt })
                .HasDatabaseName("ix_attendance_records_branch_check_in_at");
            builder.HasIndex(record => new { record.StaffId, record.CheckInAt })
                .HasDatabaseName("ix_attendance_records_staff_check_in_at");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(record => record.BranchId)
                .HasConstraintName("fk_attendance_records_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(record => record.StaffId)
                .HasConstraintName("fk_attendance_records_staff_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AttendanceQrSession>()
                .WithMany()
                .HasForeignKey(record => record.CheckInSessionId)
                .HasConstraintName("fk_attendance_records_check_in_session_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AttendanceQrSession>()
                .WithMany()
                .HasForeignKey(record => record.CheckOutSessionId)
                .HasConstraintName("fk_attendance_records_check_out_session_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
