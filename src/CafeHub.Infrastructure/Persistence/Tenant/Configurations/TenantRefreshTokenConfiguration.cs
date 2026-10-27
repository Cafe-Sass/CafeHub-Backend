using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class TenantRefreshTokenConfiguration : IEntityTypeConfiguration<TenantRefreshToken>
    {
        public void Configure(EntityTypeBuilder<TenantRefreshToken> builder)
        {
            builder.ToTable("refresh_tokens", table =>
            {
                table.HasCheckConstraint("ck_refresh_tokens_token_hash_format", "token_hash ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_refresh_tokens_expiry_after_issue", "expires_at > issued_at");
                table.HasCheckConstraint("ck_refresh_tokens_revoked_reason_length", "revoked_reason IS NULL OR (length(revoked_reason) >= 1 AND length(revoked_reason) <= 64)");
                table.HasCheckConstraint("ck_refresh_tokens_revoked_reason_labels", "revoked_reason IS NULL OR revoked_reason IN ('logout', 'reuse_detected', 'admin_revoked')");
                table.HasCheckConstraint("ck_refresh_tokens_revocation_pair", "(revoked_at IS NULL AND revoked_reason IS NULL) OR (revoked_at IS NOT NULL AND revoked_reason IS NOT NULL)");
            });

            builder.HasKey(token => token.Id).HasName("pk_refresh_tokens");
            builder.Property(token => token.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(token => token.UserId).HasColumnName("user_id").HasColumnType("uuid").IsRequired();
            builder.Property(token => token.FamilyId).HasColumnName("family_id").HasColumnType("uuid").IsRequired();
            builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasColumnType("text").IsRequired();
            builder.Property(token => token.IssuedAt).HasColumnName("issued_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(token => token.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(token => token.RotatedAt).HasColumnName("rotated_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(token => token.ReplacedById).HasColumnName("replaced_by_id").HasColumnType("uuid").IsRequired(false);
            builder.Property(token => token.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(token => token.RevokedReason).HasColumnName("revoked_reason").HasColumnType("text").IsRequired(false);

            builder.HasIndex(token => token.TokenHash).IsUnique().HasDatabaseName("uq_refresh_tokens_token_hash");
            builder.HasIndex(token => token.FamilyId).HasDatabaseName("ix_refresh_tokens_family_id");
            builder.HasIndex(token => token.UserId).HasDatabaseName("ix_refresh_tokens_user_id");
            builder.HasIndex(token => token.ReplacedById).IsUnique().HasDatabaseName("uq_refresh_tokens_replaced_by_id");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .HasConstraintName("fk_refresh_tokens_user_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantRefreshToken>()
                .WithMany()
                .HasForeignKey(token => token.ReplacedById)
                .HasConstraintName("fk_refresh_tokens_replaced_by_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
