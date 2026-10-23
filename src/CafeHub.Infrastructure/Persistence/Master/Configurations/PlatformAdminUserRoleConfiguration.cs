using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class PlatformAdminUserRoleConfiguration : IEntityTypeConfiguration<PlatformAdminUserRole>
    {
        public void Configure(EntityTypeBuilder<PlatformAdminUserRole> builder)
        {
            builder.ToTable("platform_admin_user_roles");

            builder.HasKey(ur => ur.Id).HasName("pk_platform_admin_user_roles");
            builder.Property(ur => ur.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(ur => ur.UserId).HasColumnName("platform_admin_id").HasColumnType("uuid").IsRequired();
            builder.Property(ur => ur.RoleId).HasColumnName("role_id").HasColumnType("uuid").IsRequired();
            builder.Property(ur => ur.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            builder.HasIndex(ur => new { ur.UserId, ur.RoleId })
                .IsUnique()
                .HasDatabaseName("uq_platform_admin_user_roles_user_role");

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(ur => ur.UserId)
                .HasConstraintName("fk_platform_admin_user_roles_platform_admin_id")
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<PlatformAdminRole>()
                .WithMany()
                .HasForeignKey(ur => ur.RoleId)
                .HasConstraintName("fk_platform_admin_user_roles_role_id")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

