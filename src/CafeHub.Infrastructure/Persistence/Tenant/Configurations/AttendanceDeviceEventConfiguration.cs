using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class AttendanceDeviceEventConfiguration : IEntityTypeConfiguration<AttendanceDeviceEvent>
    {
        public void Configure(EntityTypeBuilder<AttendanceDeviceEvent> builder)
        {
            builder.ToTable("attendance_device_events", table =>
            {
                table.HasCheckConstraint(
                    "ck_attendance_device_events_event_id",
                    "char_length(device_event_id) BETWEEN 1 AND 128 AND device_event_id !~ '[[:cntrl:]]'");
                table.HasCheckConstraint(
                    "ck_attendance_device_events_code",
                    "char_length(device_code) BETWEEN 1 AND 64 AND device_code !~ '[[:cntrl:]]'");
                table.HasCheckConstraint(
                    "ck_attendance_device_events_direction",
                    "direction IS NULL OR direction IN ('In', 'Out')");
                table.HasCheckConstraint(
                    "ck_attendance_device_events_status",
                    "status IN ('Processed', 'Unmatched')");
            });

            builder.HasKey(deviceEvent => deviceEvent.Id).HasName("pk_attendance_device_events");
            builder.Property(deviceEvent => deviceEvent.Id)
                .HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(deviceEvent => deviceEvent.DeviceId)
                .HasColumnName("device_id").HasColumnType("uuid");
            builder.Property(deviceEvent => deviceEvent.DeviceEventId)
                .HasColumnName("device_event_id").HasColumnType("text");
            builder.Property(deviceEvent => deviceEvent.DeviceCode)
                .HasColumnName("device_code").HasColumnType("text");
            builder.Property(deviceEvent => deviceEvent.OccurredAt)
                .HasColumnName("occurred_at").HasColumnType("timestamptz");
            builder.Property(deviceEvent => deviceEvent.Direction)
                .HasColumnName("direction").HasColumnType("text").HasConversion<string>();
            builder.Property(deviceEvent => deviceEvent.Status)
                .HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(deviceEvent => deviceEvent.RecordId)
                .HasColumnName("record_id").HasColumnType("uuid");
            builder.Property(deviceEvent => deviceEvent.ReceivedAt)
                .HasColumnName("received_at").HasColumnType("timestamptz");
            builder.Property(deviceEvent => deviceEvent.CreatedAt)
                .HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(deviceEvent => deviceEvent.UpdatedAt)
                .HasColumnName("updated_at").HasColumnType("timestamptz");

            builder.HasIndex(deviceEvent => new
                {
                    deviceEvent.DeviceId,
                    deviceEvent.Status,
                    deviceEvent.OccurredAt,
                })
                .HasDatabaseName("ix_attendance_device_events_device_status_occurred_at");
            builder.HasIndex(deviceEvent => new { deviceEvent.DeviceId, deviceEvent.DeviceEventId })
                .HasDatabaseName("uq_attendance_device_events_device_event")
                .IsUnique();
            builder.HasIndex(deviceEvent => deviceEvent.RecordId)
                .HasDatabaseName("ix_attendance_device_events_record_id");

            builder.HasOne<AttendanceDevice>()
                .WithMany()
                .HasForeignKey(deviceEvent => deviceEvent.DeviceId)
                .HasConstraintName("fk_attendance_device_events_device_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AttendanceRecord>()
                .WithMany()
                .HasForeignKey(deviceEvent => deviceEvent.RecordId)
                .HasConstraintName("fk_attendance_device_events_record_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
