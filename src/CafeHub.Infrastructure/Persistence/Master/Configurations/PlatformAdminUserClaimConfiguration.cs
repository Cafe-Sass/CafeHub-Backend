using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class PlatformAdminUserClaimConfiguration : IEntityTypeConfiguration<PlatformAdminUserClaim>
    {
        public void Configure(EntityTypeBuilder<PlatformAdminUserClaim> builder)
        {
            builder.ToTable("platform_admin_user_claims", t =>
            {
                t.HasCheckConstraint("ck_platform_admin_user_claims_claim_type_length", "length(claim_type) >= 1 AND length(claim_type) <= 512");
                t.HasCheckConstraint("ck_platform_admin_user_claims_claim_value_length", "claim_value IS NULL OR (length(claim_value) >= 0 AND length(claim_value) <= 1024)");
            });

            builder.HasKey(uc => uc.Id).HasName("pk_platform_admin_user_claims");
            builder.Property(uc => uc.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(uc => uc.UserId).HasColumnName("platform_admin_id").HasColumnType("uuid").IsRequired();
            builder.Property(uc => uc.ClaimType).HasColumnName("claim_type").HasColumnType("text").IsRequired();
            builder.Property(uc => uc.ClaimValue).HasColumnName("claim_value").HasColumnType("text").IsRequired(false);
            builder.Property(uc => uc.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            builder.HasIndex(uc => uc.UserId).HasDatabaseName("ix_platform_admin_user_claims_platform_admin_id");

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(uc => uc.UserId)
                .HasConstraintName("fk_platform_admin_user_claims_platform_admin_id")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

