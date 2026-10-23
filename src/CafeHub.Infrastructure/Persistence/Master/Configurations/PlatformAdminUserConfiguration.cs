using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class PlatformAdminUserConfiguration : IEntityTypeConfiguration<PlatformAdminUser>
    {
        public void Configure(EntityTypeBuilder<PlatformAdminUser> builder)
        {
            builder.ToTable("platform_admins", t =>
            {
                t.HasCheckConstraint("ck_platform_admins_user_name_length", "length(user_name) >= 1 AND length(user_name) <= 256");
                t.HasCheckConstraint("ck_platform_admins_normalized_user_name_length", "length(normalized_user_name) >= 1 AND length(normalized_user_name) <= 256");
                t.HasCheckConstraint("ck_platform_admins_email_length", "email IS NULL OR (length(email) >= 1 AND length(email) <= 320)");
                t.HasCheckConstraint("ck_platform_admins_normalized_email_length", "normalized_email IS NULL OR (length(normalized_email) >= 1 AND length(normalized_email) <= 320)");
                t.HasCheckConstraint("ck_platform_admins_password_hash_length", "length(password_hash) >= 1 AND length(password_hash) <= 512");
                t.HasCheckConstraint("ck_platform_admins_phone_number_length", "phone_number IS NULL OR (length(phone_number) >= 1 AND length(phone_number) <= 64)");
                t.HasCheckConstraint("ck_platform_admins_security_stamp_length", "length(security_stamp) >= 1 AND length(security_stamp) <= 64");
                t.HasCheckConstraint("ck_platform_admins_concurrency_stamp_length", "length(concurrency_stamp) >= 1 AND length(concurrency_stamp) <= 64");
            });

            builder.HasKey(u => u.Id).HasName("pk_platform_admins");
            builder.Property(u => u.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(u => u.UserName).HasColumnName("user_name").HasColumnType("text").IsRequired();
            builder.Property(u => u.NormalizedUserName).HasColumnName("normalized_user_name").HasColumnType("text").IsRequired();
            builder.Property(u => u.Email).HasColumnName("email").HasColumnType("text").IsRequired(false);
            builder.Property(u => u.NormalizedEmail).HasColumnName("normalized_email").HasColumnType("text").IsRequired(false);
            builder.Property(u => u.EmailConfirmed).HasColumnName("email_confirmed").HasColumnType("boolean").IsRequired().HasDefaultValue(false);
            builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasColumnType("text").IsRequired();
            builder.Property(u => u.PhoneNumber).HasColumnName("phone_number").HasColumnType("text").IsRequired(false);
            builder.Property(u => u.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed").HasColumnType("boolean").IsRequired().HasDefaultValue(false);
            builder.Property(u => u.TwoFactorEnabled).HasColumnName("two_factor_enabled").HasColumnType("boolean").IsRequired().HasDefaultValue(false);
            builder.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled").HasColumnType("boolean").IsRequired().HasDefaultValue(true);
            builder.Property(u => u.LockoutEnd).HasColumnName("lockout_end_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count").HasColumnType("integer").IsRequired().HasDefaultValue(0);
            builder.Property(u => u.SecurityStamp).HasColumnName("security_stamp").HasColumnType("text").IsRequired();
            builder.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp").HasColumnType("text").IsRequired().IsConcurrencyToken();

            builder.Property(u => u.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(u => u.DeletedBy).HasColumnName("deleted_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(u => u.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(u => u.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(u => u.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(u => u.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.Ignore(u => u.IsActive);

            builder.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("uq_platform_admins_normalized_user_name");

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(u => u.DeletedBy)
                .HasConstraintName("fk_platform_admins_deleted_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(u => u.CreatedBy)
                .HasConstraintName("fk_platform_admins_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(u => u.UpdatedBy)
                .HasConstraintName("fk_platform_admins_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

