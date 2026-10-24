using CafeHub.Domain.Identity;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("refresh_tokens", t =>
            {
                t.HasCheckConstraint("ck_refresh_tokens_token_hash_format", "token_hash ~ '^[0-9a-f]{64}$'");
                t.HasCheckConstraint("ck_refresh_tokens_expiry_after_issue", "expires_at > issued_at");
                t.HasCheckConstraint("ck_refresh_tokens_revoked_reason_length", "revoked_reason IS NULL OR (length(revoked_reason) >= 1 AND length(revoked_reason) <= 64)");
                t.HasCheckConstraint("ck_refresh_tokens_revoked_reason_labels", "revoked_reason IS NULL OR revoked_reason IN ('logout', 'reuse_detected', 'admin_revoked')");
                t.HasCheckConstraint("ck_refresh_tokens_revocation_pair", "(revoked_at IS NULL AND revoked_reason IS NULL) OR (revoked_at IS NOT NULL AND revoked_reason IS NOT NULL)");
            });

            builder.HasKey(r => r.Id).HasName("pk_refresh_tokens");
            builder.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(r => r.AdminId).HasColumnName("admin_id").HasColumnType("uuid").IsRequired();
            builder.Property(r => r.FamilyId).HasColumnName("family_id").HasColumnType("uuid").IsRequired();
            builder.Property(r => r.TokenHash).HasColumnName("token_hash").HasColumnType("text").IsRequired();
            builder.Property(r => r.IssuedAt).HasColumnName("issued_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(r => r.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(r => r.RotatedAt).HasColumnName("rotated_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(r => r.ReplacedById).HasColumnName("replaced_by_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(r => r.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(r => r.RevokedReason).HasColumnName("revoked_reason").HasColumnType("text").IsRequired(false);

            builder.HasIndex(r => r.TokenHash).IsUnique().HasDatabaseName("uq_refresh_tokens_token_hash");
            builder.HasIndex(r => r.FamilyId).HasDatabaseName("ix_refresh_tokens_family_id");
            builder.HasIndex(r => r.AdminId).HasDatabaseName("ix_refresh_tokens_admin_id");
            builder.HasIndex(r => r.ReplacedById).IsUnique().HasDatabaseName("uq_refresh_tokens_replaced_by_id");

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(r => r.AdminId)
                .HasConstraintName("fk_refresh_tokens_admin_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<RefreshToken>()
                .WithMany()
                .HasForeignKey(r => r.ReplacedById)
                .HasConstraintName("fk_refresh_tokens_replaced_by_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
