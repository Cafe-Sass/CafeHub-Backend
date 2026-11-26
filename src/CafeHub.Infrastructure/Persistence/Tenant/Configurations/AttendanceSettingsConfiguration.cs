using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class AttendanceSettingsConfiguration : IEntityTypeConfiguration<AttendanceSettings>
    {
        public void Configure(EntityTypeBuilder<AttendanceSettings> builder)
        {
            builder.ToTable("attendance_settings", table =>
            {
                table.HasCheckConstraint(
                    "ck_attendance_settings_mode", "mode IN ('Off', 'Qr', 'Device')");
            });

            builder.HasKey(settings => settings.Id).HasName("pk_attendance_settings");
            builder.Property(settings => settings.Id)
                .HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(settings => settings.Mode)
                .HasColumnName("mode").HasColumnType("text").HasConversion<string>()
                .HasDefaultValue(AttendanceMode.Off).IsRequired();
            builder.Property(settings => settings.CreatedAt)
                .HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(settings => settings.UpdatedAt)
                .HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(settings => settings.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(settings => settings.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(settings => settings.CreatedBy)
                .HasDatabaseName("ix_attendance_settings_created_by");
            builder.HasIndex(settings => settings.UpdatedBy)
                .HasDatabaseName("ix_attendance_settings_updated_by");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(settings => settings.CreatedBy)
                .HasConstraintName("fk_attendance_settings_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(settings => settings.UpdatedBy)
                .HasConstraintName("fk_attendance_settings_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
