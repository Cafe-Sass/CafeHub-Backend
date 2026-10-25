using CafeHub.Domain.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class PlanEntitlementConfiguration : IEntityTypeConfiguration<PlanEntitlement>
    {
        public void Configure(EntityTypeBuilder<PlanEntitlement> builder)
        {
            builder.ToTable("plan_entitlements", t =>
            {
                t.HasCheckConstraint("ck_plan_entitlements_entitlement_key_format", "entitlement_key ~ '^[a-z][a-z0-9_.]*$'");
                t.HasCheckConstraint("ck_plan_entitlements_entitlement_key_length", "length(entitlement_key) >= 1 AND length(entitlement_key) <= 128");
                t.HasCheckConstraint("ck_plan_entitlements_value_length", "length(value) >= 1 AND length(value) <= 512");
            });

            builder.HasKey(e => e.Id).HasName("pk_plan_entitlements");
            builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(e => e.PlanVersionId).HasColumnName("plan_version_id").HasColumnType("uuid").IsRequired();
            builder.Property(e => e.Key).HasColumnName("entitlement_key").HasColumnType("text").IsRequired();
            builder.Property(e => e.Value).HasColumnName("value").HasColumnType("text").IsRequired();
            builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            builder.HasIndex(e => new { e.PlanVersionId, e.Key }).IsUnique().HasDatabaseName("uq_plan_entitlements_key");
        }
    }
}
