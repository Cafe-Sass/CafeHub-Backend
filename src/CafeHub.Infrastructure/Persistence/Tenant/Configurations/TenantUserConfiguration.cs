using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
    {
        public void Configure(EntityTypeBuilder<TenantUser> builder)
        {
            builder.ToTable("tenant_users", t =>
            {
                t.HasCheckConstraint("ck_tenant_users_kind", "user_kind IN ('Owner', 'Staff')");
                t.HasCheckConstraint("ck_tenant_users_user_name_length", "length(user_name) >= 1 AND length(user_name) <= 256");
                t.HasCheckConstraint("ck_tenant_users_normalized_user_name_length", "length(normalized_user_name) >= 1 AND length(normalized_user_name) <= 256");
                t.HasCheckConstraint("ck_tenant_users_full_name_length", "length(full_name) >= 1 AND length(full_name) <= 256");
                t.HasCheckConstraint("ck_tenant_users_email_length", "email IS NULL OR (length(email) >= 1 AND length(email) <= 320)");
                t.HasCheckConstraint("ck_tenant_users_normalized_email_length", "normalized_email IS NULL OR (length(normalized_email) >= 1 AND length(normalized_email) <= 320)");
                t.HasCheckConstraint("ck_tenant_users_password_hash_length", "length(password_hash) >= 1 AND length(password_hash) <= 512");
                t.HasCheckConstraint("ck_tenant_users_phone_number_length", "phone_number IS NULL OR (length(phone_number) >= 1 AND length(phone_number) <= 64)");
                t.HasCheckConstraint("ck_tenant_users_security_stamp_length", "length(security_stamp) >= 1 AND length(security_stamp) <= 64");
                t.HasCheckConstraint("ck_tenant_users_concurrency_stamp_length", "length(concurrency_stamp) >= 1 AND length(concurrency_stamp) <= 64");
            });

            builder.HasKey(u => u.Id).HasName("pk_tenant_users");
            builder.Property(u => u.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(u => u.UserKind).HasColumnName("user_kind").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(u => u.UserName).HasColumnName("user_name").HasColumnType("text").IsRequired();
            builder.Property(u => u.NormalizedUserName).HasColumnName("normalized_user_name").HasColumnType("text").IsRequired();
            builder.Property(u => u.FullName).HasColumnName("full_name").HasColumnType("text").IsRequired();
            builder.Property(u => u.Email).HasColumnName("email").HasColumnType("text").IsRequired(false);
            builder.Property(u => u.NormalizedEmail).HasColumnName("normalized_email").HasColumnType("text").IsRequired(false);
            builder.Property(u => u.EmailConfirmed).HasColumnName("email_confirmed").HasColumnType("boolean").IsRequired().HasDefaultValue(false);
            builder.Property(u => u.PhoneNumber).HasColumnName("phone_number").HasColumnType("text").IsRequired(false);
            builder.Property(u => u.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed").HasColumnType("boolean").IsRequired().HasDefaultValue(false);
            builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasColumnType("text").IsRequired();
            builder.Property(u => u.TwoFactorEnabled).HasColumnName("two_factor_enabled").HasColumnType("boolean").IsRequired().HasDefaultValue(false);
            builder.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled").HasColumnType("boolean").IsRequired().HasDefaultValue(true);
            builder.Property(u => u.LockoutEndAt).HasColumnName("lockout_end_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count").HasColumnType("integer").IsRequired().HasDefaultValue(0);
            builder.Property(u => u.SecurityStamp).HasColumnName("security_stamp").HasColumnType("text").IsRequired();
            builder.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp").HasColumnType("text").IsRequired().IsConcurrencyToken();

            builder.Property(u => u.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(u => u.DeletedBy).HasColumnName("deleted_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(u => u.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(u => u.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(u => u.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(u => u.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("uq_tenant_users_normalized_user_name");
            builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_tenant_users_normalized_email");
            builder.HasIndex(u => u.CreatedBy).HasDatabaseName("ix_tenant_users_created_by");
            builder.HasIndex(u => u.UpdatedBy).HasDatabaseName("ix_tenant_users_updated_by");
            builder.HasIndex(u => u.DeletedBy).HasDatabaseName("ix_tenant_users_deleted_by");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(u => u.DeletedBy)
                .HasConstraintName("fk_tenant_users_deleted_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(u => u.CreatedBy)
                .HasConstraintName("fk_tenant_users_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(u => u.UpdatedBy)
                .HasConstraintName("fk_tenant_users_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
