using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class TenantUserRoleConfiguration : IEntityTypeConfiguration<TenantUserRole>
    {
        public void Configure(EntityTypeBuilder<TenantUserRole> builder)
        {
            builder.ToTable("tenant_user_roles");

            builder.HasKey(r => r.Id).HasName("pk_tenant_user_roles");
            builder.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(r => r.TenantUserId).HasColumnName("tenant_user_id").HasColumnType("uuid").IsRequired();
            builder.Property(r => r.RoleId).HasColumnName("role_id").HasColumnType("uuid").IsRequired();
            builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            builder.HasIndex(r => new { r.TenantUserId, r.RoleId }).IsUnique().HasDatabaseName("uq_tenant_user_roles_user_role");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(r => r.TenantUserId)
                .HasConstraintName("fk_tenant_user_roles_tenant_user_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantRole>()
                .WithMany()
                .HasForeignKey(r => r.RoleId)
                .HasConstraintName("fk_tenant_user_roles_role_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
