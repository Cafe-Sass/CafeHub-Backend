using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class CafePaymentSettingsConfiguration : IEntityTypeConfiguration<CafePaymentSettings>
    {
        public void Configure(EntityTypeBuilder<CafePaymentSettings> builder)
        {
            builder.ToTable("cafe_payment_settings", t =>
            {
                t.HasCheckConstraint(
                    "ck_cafe_payment_settings_enable_needs_destination",
                    "NOT online_enabled OR payment_link IS NOT NULL OR payment_address IS NOT NULL");
                t.HasCheckConstraint(
                    "ck_cafe_payment_settings_method_label_length",
                    "length(method_label) >= 1 AND length(method_label) <= 32");
                t.HasCheckConstraint(
                    "ck_cafe_payment_settings_payment_link_format",
                    "payment_link IS NULL OR (payment_link ~ '^https://' AND length(payment_link) <= 512)");
                t.HasCheckConstraint(
                    "ck_cafe_payment_settings_payment_address_format",
                    "payment_address IS NULL OR (length(payment_address) >= 1 AND length(payment_address) <= 128 AND payment_address !~ '[[:cntrl:]]')");
                t.HasCheckConstraint(
                    "ck_cafe_payment_settings_display_name_format",
                    "display_name IS NULL OR (length(display_name) >= 1 AND length(display_name) <= 128 AND display_name !~ '[[:cntrl:]]')");
            });

            builder.HasKey(s => s.Id).HasName("pk_cafe_payment_settings");
            builder.Property(s => s.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(s => s.OnlineEnabled).HasColumnName("online_enabled").HasColumnType("boolean")
                .HasDefaultValue(false).IsRequired();
            builder.Property(s => s.MethodLabel).HasColumnName("method_label").HasColumnType("text")
                .HasDefaultValue(CafePaymentSettings.DefaultMethodLabel).IsRequired();
            builder.Property(s => s.PaymentLink).HasColumnName("payment_link").HasColumnType("text").IsRequired(false);
            builder.Property(s => s.PaymentAddress).HasColumnName("payment_address").HasColumnType("text").IsRequired(false);
            builder.Property(s => s.DisplayName).HasColumnName("display_name").HasColumnType("text").IsRequired(false);

            builder.Property(s => s.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(s => s.CreatedBy).HasDatabaseName("ix_cafe_payment_settings_created_by");
            builder.HasIndex(s => s.UpdatedBy).HasDatabaseName("ix_cafe_payment_settings_updated_by");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(s => s.CreatedBy)
                .HasConstraintName("fk_cafe_payment_settings_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(s => s.UpdatedBy)
                .HasConstraintName("fk_cafe_payment_settings_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
