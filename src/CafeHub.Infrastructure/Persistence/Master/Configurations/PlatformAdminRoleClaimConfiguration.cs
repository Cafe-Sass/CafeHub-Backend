using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class PlatformAdminRoleClaimConfiguration : IEntityTypeConfiguration<PlatformAdminRoleClaim>
    {
        public void Configure(EntityTypeBuilder<PlatformAdminRoleClaim> builder)
        {
            builder.ToTable("platform_admin_role_claims", t =>
            {
                t.HasCheckConstraint("ck_platform_admin_role_claims_claim_type_length", "length(claim_type) >= 1 AND length(claim_type) <= 512");
                t.HasCheckConstraint("ck_platform_admin_role_claims_claim_value_length", "claim_value IS NULL OR (length(claim_value) >= 0 AND length(claim_value) <= 1024)");
            });

            builder.HasKey(rc => rc.Id).HasName("pk_platform_admin_role_claims");
            builder.Property(rc => rc.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(rc => rc.RoleId).HasColumnName("role_id").HasColumnType("uuid").IsRequired();
            builder.Property(rc => rc.ClaimType).HasColumnName("claim_type").HasColumnType("text").IsRequired();
            builder.Property(rc => rc.ClaimValue).HasColumnName("claim_value").HasColumnType("text").IsRequired(false);
            builder.Property(rc => rc.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            builder.HasIndex(rc => rc.RoleId).HasDatabaseName("ix_platform_admin_role_claims_role_id");

            builder.HasOne<PlatformAdminRole>()
                .WithMany()
                .HasForeignKey(rc => rc.RoleId)
                .HasConstraintName("fk_platform_admin_role_claims_role_id")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

