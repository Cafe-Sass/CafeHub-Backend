using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class CafeSettingsConfiguration : IEntityTypeConfiguration<CafeSettings>
    {
        public void Configure(EntityTypeBuilder<CafeSettings> builder)
        {
            builder.ToTable("cafe_settings", t =>
            {
                t.HasCheckConstraint("ck_cafe_settings_cafe_name_length", "length(cafe_name) >= 1 AND length(cafe_name) <= 256");
                t.HasCheckConstraint("ck_cafe_settings_logo_media_reference_length", "logo_media_reference IS NULL OR length(logo_media_reference) <= 512");
                t.HasCheckConstraint("ck_cafe_settings_color_format", "primary_color IS NULL OR primary_color ~ '^#[0-9a-fA-F]{6}$' AND secondary_color IS NULL OR secondary_color ~ '^#[0-9a-fA-F]{6}$' AND accent_color IS NULL OR accent_color ~ '^#[0-9a-fA-F]{6}$'");
                t.HasCheckConstraint("ck_cafe_settings_contact_phone_length", "contact_phone IS NULL OR length(contact_phone) <= 32");
                t.HasCheckConstraint("ck_cafe_settings_contact_email_length", "contact_email IS NULL OR length(contact_email) <= 320");
                t.HasCheckConstraint("ck_cafe_settings_contact_address_length", "contact_address IS NULL OR length(contact_address) <= 512");
                t.HasCheckConstraint("ck_cafe_settings_timezone", "length(timezone) >= 1 AND length(timezone) <= 64");
            });

            builder.HasKey(s => s.Id).HasName("pk_cafe_settings");
            builder.Property(s => s.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(s => s.CafeName).HasColumnName("cafe_name").HasColumnType("text").IsRequired();
            builder.Property(s => s.LogoMediaReference).HasColumnName("logo_media_reference").HasColumnType("text").IsRequired(false);
            builder.Property(s => s.PrimaryColor).HasColumnName("primary_color").HasColumnType("text").IsRequired(false);
            builder.Property(s => s.SecondaryColor).HasColumnName("secondary_color").HasColumnType("text").IsRequired(false);
            builder.Property(s => s.AccentColor).HasColumnName("accent_color").HasColumnType("text").IsRequired(false);
            builder.Property(s => s.ContactPhone).HasColumnName("contact_phone").HasColumnType("text").IsRequired(false);
            builder.Property(s => s.ContactEmail).HasColumnName("contact_email").HasColumnType("text").IsRequired(false);
            builder.Property(s => s.ContactAddress).HasColumnName("contact_address").HasColumnType("text").IsRequired(false);
            builder.Property(s => s.Timezone).HasColumnName("timezone").HasColumnType("text").IsRequired();

            builder.Property(s => s.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(s => s.CreatedBy).HasDatabaseName("ix_cafe_settings_created_by");
            builder.HasIndex(s => s.UpdatedBy).HasDatabaseName("ix_cafe_settings_updated_by");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(s => s.CreatedBy)
                .HasConstraintName("fk_cafe_settings_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(s => s.UpdatedBy)
                .HasConstraintName("fk_cafe_settings_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
