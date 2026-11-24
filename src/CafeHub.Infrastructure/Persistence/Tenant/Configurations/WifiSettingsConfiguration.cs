using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class WifiSettingsConfiguration : IEntityTypeConfiguration<WifiSettings>
    {
        public void Configure(EntityTypeBuilder<WifiSettings> builder)
        {
            builder.ToTable("wifi_settings", table =>
            {
                table.HasCheckConstraint("ck_wifi_settings_policy", "policy IN ('Free', 'Paid')");
            });

            builder.HasKey(settings => settings.Id).HasName("pk_wifi_settings");
            builder.Property(settings => settings.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(settings => settings.Policy)
                .HasColumnName("policy").HasColumnType("text").HasConversion<string>().HasDefaultValue(WifiPolicy.Free);
            builder.Property(settings => settings.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(settings => settings.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(settings => settings.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(settings => settings.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(settings => settings.CreatedBy).HasDatabaseName("ix_wifi_settings_created_by");
            builder.HasIndex(settings => settings.UpdatedBy).HasDatabaseName("ix_wifi_settings_updated_by");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(settings => settings.CreatedBy)
                .HasConstraintName("fk_wifi_settings_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(settings => settings.UpdatedBy)
                .HasConstraintName("fk_wifi_settings_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
