using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class TenantRoleClaimConfiguration : IEntityTypeConfiguration<TenantRoleClaim>
    {
        public void Configure(EntityTypeBuilder<TenantRoleClaim> builder)
        {
            builder.ToTable("tenant_role_claims");

            builder.HasKey(c => c.Id).HasName("pk_tenant_role_claims");
            builder.Property(c => c.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(c => c.RoleId).HasColumnName("role_id").HasColumnType("uuid").IsRequired();
            builder.Property(c => c.ClaimType).HasColumnName("claim_type").HasColumnType("text").IsRequired();
            builder.Property(c => c.ClaimValue).HasColumnName("claim_value").HasColumnType("text").IsRequired(false);
            builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            builder.HasOne<TenantRole>()
                .WithMany()
                .HasForeignKey(c => c.RoleId)
                .HasConstraintName("fk_tenant_role_claims_role_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
