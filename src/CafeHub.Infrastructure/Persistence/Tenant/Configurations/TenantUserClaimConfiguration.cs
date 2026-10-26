using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class TenantUserClaimConfiguration : IEntityTypeConfiguration<TenantUserClaim>
    {
        public void Configure(EntityTypeBuilder<TenantUserClaim> builder)
        {
            builder.ToTable("tenant_user_claims");

            builder.HasKey(c => c.Id).HasName("pk_tenant_user_claims");
            builder.Property(c => c.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(c => c.TenantUserId).HasColumnName("tenant_user_id").HasColumnType("uuid").IsRequired();
            builder.Property(c => c.ClaimType).HasColumnName("claim_type").HasColumnType("text").IsRequired();
            builder.Property(c => c.ClaimValue).HasColumnName("claim_value").HasColumnType("text").IsRequired(false);
            builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(c => c.TenantUserId)
                .HasConstraintName("fk_tenant_user_claims_tenant_user_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
