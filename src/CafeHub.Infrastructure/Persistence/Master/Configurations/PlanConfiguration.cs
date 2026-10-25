using CafeHub.Domain.Plans;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
    {
        public void Configure(EntityTypeBuilder<Plan> builder)
        {
            builder.ToTable("plans", t =>
            {
                t.HasCheckConstraint("ck_plans_code_format", "code ~ '^[a-z][a-z0-9-]{2,63}$'");
                t.HasCheckConstraint("ck_plans_display_name_length", "length(display_name) >= 1 AND length(display_name) <= 256");
            });

            builder.HasKey(p => p.Id).HasName("pk_plans");
            builder.Property(p => p.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(p => p.Code).HasColumnName("code").HasColumnType("text").IsRequired();
            builder.Property(p => p.DisplayName).HasColumnName("display_name").HasColumnType("text").IsRequired();
            builder.Property(p => p.IsSelectable).HasColumnName("is_selectable").HasColumnType("boolean").IsRequired().HasDefaultValue(false);
            builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(p => p.Code).IsUnique().HasDatabaseName("uq_plans_code");
            builder.HasIndex(p => p.CreatedBy).HasDatabaseName("ix_plans_created_by");
            builder.HasIndex(p => p.UpdatedBy).HasDatabaseName("ix_plans_updated_by");

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(p => p.CreatedBy)
                .HasConstraintName("fk_plans_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(p => p.UpdatedBy)
                .HasConstraintName("fk_plans_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(p => p.Versions)
                .WithOne()
                .HasForeignKey(v => v.PlanId)
                .HasConstraintName("fk_plan_versions_plan_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(p => p.Versions)
                .HasField("versions")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
