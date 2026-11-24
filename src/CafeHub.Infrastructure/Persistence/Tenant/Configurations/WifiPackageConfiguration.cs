using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class WifiPackageConfiguration : IEntityTypeConfiguration<WifiPackage>
    {
        public void Configure(EntityTypeBuilder<WifiPackage> builder)
        {
            builder.ToTable("wifi_packages", table =>
            {
                table.HasCheckConstraint(
                    "ck_wifi_packages_name",
                    "char_length(name) BETWEEN 1 AND 48 AND name !~ '[[:cntrl:]]'");
                table.HasCheckConstraint(
                    "ck_wifi_packages_duration", "duration_minutes BETWEEN 1 AND 43200");
                table.HasCheckConstraint(
                    "ck_wifi_packages_speed",
                    "speed_mbps IS NULL OR (speed_mbps > 0 AND speed_mbps <= 999999.99)");
                table.HasCheckConstraint(
                    "ck_wifi_packages_data_mode", "data_mode IN ('Unlimited', 'Limited')");
                table.HasCheckConstraint(
                    "ck_wifi_packages_quota", "quota_mb IS NULL OR quota_mb BETWEEN 1 AND 1000000");
                table.HasCheckConstraint(
                    "ck_wifi_packages_quota_mode", "(data_mode = 'Limited') = (quota_mb IS NOT NULL)");
                table.HasCheckConstraint("ck_wifi_packages_price", "price >= 0");
                table.HasCheckConstraint("ck_wifi_packages_sort_order", "sort_order >= 0");
                table.HasCheckConstraint(
                    "ck_wifi_packages_status", "status IN ('Active', 'Inactive')");
            });

            builder.HasKey(package => package.Id).HasName("pk_wifi_packages");
            builder.Property(package => package.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(package => package.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(package => package.DurationMinutes).HasColumnName("duration_minutes").HasColumnType("integer");
            builder.Property(package => package.SpeedMbps).HasColumnName("speed_mbps").HasColumnType("numeric(9,2)").HasPrecision(9, 2);
            builder.Property(package => package.DataMode)
                .HasColumnName("data_mode").HasColumnType("text").HasConversion<string>();
            builder.Property(package => package.QuotaMb).HasColumnName("quota_mb").HasColumnType("integer");
            builder.Property(package => package.Price).HasColumnName("price").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(package => package.SortOrder).HasColumnName("sort_order").HasColumnType("integer");
            builder.Property(package => package.Status)
                .HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(package => package.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(package => package.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(package => package.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(package => package.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(package => package.CreatedBy).HasDatabaseName("ix_wifi_packages_created_by");
            builder.HasIndex(package => package.UpdatedBy).HasDatabaseName("ix_wifi_packages_updated_by");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(package => package.CreatedBy)
                .HasConstraintName("fk_wifi_packages_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(package => package.UpdatedBy)
                .HasConstraintName("fk_wifi_packages_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
