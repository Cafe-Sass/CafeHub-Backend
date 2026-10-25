using CafeHub.Domain.Plans;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class PlanVersionConfiguration : IEntityTypeConfiguration<PlanVersion>
    {
        public void Configure(EntityTypeBuilder<PlanVersion> builder)
        {
            builder.ToTable("plan_versions", t =>
            {
                t.HasCheckConstraint("ck_plan_versions_version_number_positive", "version_number >= 1");
                t.HasCheckConstraint("ck_plan_versions_billing_period", "billing_period IN ('Monthly', 'Annual')");
                t.HasCheckConstraint("ck_plan_versions_price_non_negative", "price >= 0");
                t.HasCheckConstraint("ck_plan_versions_currency_code_format", "currency_code ~ '^[a-z]{3}$'");
                t.HasCheckConstraint("ck_plan_versions_status", "status IN ('Draft', 'Active', 'Retired')");
            });

            builder.HasKey(v => v.Id).HasName("pk_plan_versions");
            builder.Property(v => v.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(v => v.PlanId).HasColumnName("plan_id").HasColumnType("uuid").IsRequired();
            builder.Property(v => v.VersionNumber).HasColumnName("version_number").HasColumnType("integer").IsRequired();
            builder.Property(v => v.BillingPeriod).HasColumnName("billing_period").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(v => v.Price).HasColumnName("price").HasColumnType("numeric(12,2)").HasPrecision(12, 2).IsRequired();
            builder.Property(v => v.CurrencyCode).HasColumnName("currency_code").HasColumnType("text").IsRequired();
            builder.Property(v => v.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(v => v.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(v => v.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(v => v.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(v => v.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(v => new { v.PlanId, v.VersionNumber }).IsUnique().HasDatabaseName("uq_plan_versions_plan_version");
            builder.HasIndex(v => v.CreatedBy).HasDatabaseName("ix_plan_versions_created_by");
            builder.HasIndex(v => v.UpdatedBy).HasDatabaseName("ix_plan_versions_updated_by");

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(v => v.CreatedBy)
                .HasConstraintName("fk_plan_versions_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(v => v.UpdatedBy)
                .HasConstraintName("fk_plan_versions_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(v => v.Entitlements)
                .WithOne()
                .HasForeignKey(e => e.PlanVersionId)
                .HasConstraintName("fk_plan_entitlements_plan_version_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(v => v.Entitlements)
                .HasField("entitlements")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
