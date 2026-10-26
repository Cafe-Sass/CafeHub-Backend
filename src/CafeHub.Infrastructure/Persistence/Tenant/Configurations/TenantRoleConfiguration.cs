using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class TenantRoleConfiguration : IEntityTypeConfiguration<TenantRole>
    {
        public void Configure(EntityTypeBuilder<TenantRole> builder)
        {
            builder.ToTable("tenant_roles", t =>
            {
                t.HasCheckConstraint("ck_tenant_roles_name_length", "length(name) >= 1 AND length(name) <= 256");
                t.HasCheckConstraint("ck_tenant_roles_normalized_name_length", "length(normalized_name) >= 1 AND length(normalized_name) <= 256");
                t.HasCheckConstraint("ck_tenant_roles_concurrency_stamp_length", "length(concurrency_stamp) >= 1 AND length(concurrency_stamp) <= 64");
            });

            builder.HasKey(r => r.Id).HasName("pk_tenant_roles");
            builder.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(r => r.Name).HasColumnName("name").HasColumnType("text").IsRequired();
            builder.Property(r => r.NormalizedName).HasColumnName("normalized_name").HasColumnType("text").IsRequired();
            builder.Property(r => r.ConcurrencyStamp).HasColumnName("concurrency_stamp").HasColumnType("text").IsRequired().IsConcurrencyToken();

            builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(r => r.NormalizedName).IsUnique().HasDatabaseName("uq_tenant_roles_normalized_name");
            builder.HasIndex(r => r.CreatedBy).HasDatabaseName("ix_tenant_roles_created_by");
            builder.HasIndex(r => r.UpdatedBy).HasDatabaseName("ix_tenant_roles_updated_by");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(r => r.CreatedBy)
                .HasConstraintName("fk_tenant_roles_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(r => r.UpdatedBy)
                .HasConstraintName("fk_tenant_roles_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
